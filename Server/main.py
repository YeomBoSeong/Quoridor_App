# main.py
from fastapi import FastAPI, HTTPException, Depends, WebSocket, WebSocketDisconnect, File, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import OAuth2PasswordRequestForm
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from sqlalchemy import create_engine, select, func, update
from sqlalchemy.orm import sessionmaker, Session
from heapq import heappop, heappush
import time
import os
import uuid
from pathlib import Path
from datetime import datetime
from jose import jwt, JWTError

# 로컬 모듈 import
from User import (
    Base, User, Friendship, FriendRequest, ChatMessage, BattleRequest, GameHistory, GameMove, GameCredit,
    SignupRequest, Token, MeResponse, ProfileResponse,
    AddFriendRequest, FriendRequestResponse, FriendRequestData, FriendData, FriendsListResponse,
    ChatMessageRequest, ChatMessageData, ChatMessageResponse, ChatHistoryResponse,
    UnreadMessageData, UnreadMessagesResponse,
    GameMoveData, GameHistoryData, GameHistoryResponse, GameDetailResponse,
    UserStatusRequest, UserStatusResponse, BattleRequestData, BattleRequestResponse,
    VerificationRequest, VerificationResponse, VerifyCodeRequest, VerifyCodeResponse,
    ForgotPasswordRequest, ForgotPasswordResponse,
    ChangePasswordRequest, ChangePasswordResponse,
    GameCreditResponse, ConsumeGameResponse,
    DeleteAccountRequest, DeleteAccountResponse,
    create_user, authenticate_user, get_current_user_factory, create_access_token,
    update_user_session, calculate_percentile, calculate_elo_change, update_user_elo,
    get_user_elos, SECRET_KEY, ALGORITHM, hash_password, verify_password
)
from email_sender import generate_verification_code, send_verification_email, generate_temporary_password, send_account_recovery_email
from quoridor_ai import QuoridorAI

# ================== 데이터베이스 설정 ==================
DATABASE_URL = "sqlite:///./quoridor.db"

# ================== 파일 업로드 설정 ==================
UPLOAD_DIRECTORY = "uploads"
PROFILE_IMAGES_DIR = os.path.join(UPLOAD_DIRECTORY, "profile_images")

# 업로드 디렉토리 생성
os.makedirs(PROFILE_IMAGES_DIR, exist_ok=True)

engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False)

# 데이터베이스 테이블 생성
Base.metadata.create_all(bind=engine)

def get_db() -> Session:
    db = SessionLocal()
    try:
        yield db
    except Exception as e:
        print(f"Database session error: {e}")
        db.rollback()
        raise
    finally:
        db.close()

# ================== 앱 설정 ==================
app = FastAPI(title="Quoridor Auth API", version="1.0.0")

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Static files (계정 삭제 페이지 등)
app.mount("/static", StaticFiles(directory="static"), name="static")

# get_current_user 함수 생성
get_current_user = get_current_user_factory(get_db)

# ================== 이메일 인증 관련 ==================
# 임시 인증 데이터 저장소 (email -> {username, password, code, expires_at})
pending_verifications = {}

# ================== 사용자 인증 엔드포인트 ==================
@app.post("/signup", response_model=MeResponse)
def signup(req: SignupRequest, db: Session = Depends(get_db)):
    print(f"Signup request received for username: {req.username}, email: {req.email}")
    
    try:
        user = create_user(db, req)
        print(f"User created successfully with ID: {user.id}")
        return MeResponse(id=user.id, username=user.username, email=user.email, created_at=user.created_at)
        
    except HTTPException as he:
        print(f"HTTP Exception: {he.detail}")
        raise
    except Exception as e:
        print(f"Unexpected signup error: {str(e)}")
        print(f"Error type: {type(e).__name__}")
        db.rollback()
        raise HTTPException(status_code=500, detail=f"Account creation failed: {str(e)}")

@app.post("/request-verification", response_model=VerificationResponse)
def request_verification(req: VerificationRequest, db: Session = Depends(get_db)):
    """이메일 인증 요청 - 6자리 코드를 이메일로 전송"""
    from datetime import timedelta

    print(f"Verification request for username: {req.username}, email: {req.email}")

    try:
        # 중복 확인
        u_exists = db.execute(select(User).where(User.username == req.username)).scalar_one_or_none()
        if u_exists:
            raise HTTPException(status_code=409, detail="Username already exists")

        e_exists = db.execute(select(User).where(User.email == req.email)).scalar_one_or_none()
        if e_exists:
            raise HTTPException(status_code=409, detail="Email already exists")

        # 6자리 인증 코드 생성
        verification_code = generate_verification_code()

        # 이메일 발송
        email_sent = send_verification_email(req.email, verification_code)
        if not email_sent:
            raise HTTPException(status_code=500, detail="Failed to send verification email")

        # 임시 저장 (10분 만료)
        expires_at = datetime.utcnow() + timedelta(minutes=10)
        pending_verifications[req.email] = {
            "username": req.username,
            "password": hash_password(req.password),  # 비밀번호는 해시해서 저장
            "code": verification_code,
            "expires_at": expires_at
        }

        print(f"Verification code {verification_code} sent to {req.email}, expires at {expires_at}")

        return VerificationResponse(
            success=True,
            message="Verification code sent to your email"
        )

    except HTTPException as he:
        print(f"HTTP Exception: {he.detail}")
        raise
    except Exception as e:
        print(f"Unexpected error during verification request: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Verification request failed: {str(e)}")

@app.post("/verify-code", response_model=VerifyCodeResponse)
def verify_code(req: VerifyCodeRequest, db: Session = Depends(get_db)):
    """인증 코드 검증 및 계정 생성"""
    print(f"Verification code check for email: {req.email}, code: {req.code}")

    try:
        # 임시 저장소에서 데이터 확인
        if req.email not in pending_verifications:
            raise HTTPException(status_code=404, detail="No verification request found for this email")

        verification_data = pending_verifications[req.email]

        # 만료 확인
        if datetime.utcnow() > verification_data["expires_at"]:
            del pending_verifications[req.email]
            raise HTTPException(status_code=400, detail="Verification code expired")

        # 코드 확인
        if req.code != verification_data["code"]:
            raise HTTPException(status_code=400, detail="Invalid verification code")

        # 계정 생성
        user = User(
            username=verification_data["username"],
            password=verification_data["password"],  # 이미 해시된 비밀번호
            email=req.email,
            elo_rating=1500,
            created_at=datetime.utcnow()
        )

        db.add(user)
        db.commit()
        db.refresh(user)

        # 임시 데이터 삭제
        del pending_verifications[req.email]

        print(f"User {user.username} created successfully with ID: {user.id}")

        return VerifyCodeResponse(
            success=True,
            message="Account created successfully",
            user_id=user.id,
            username=user.username
        )

    except HTTPException as he:
        print(f"HTTP Exception: {he.detail}")
        raise
    except Exception as e:
        print(f"Unexpected error during verification: {str(e)}")
        db.rollback()
        raise HTTPException(status_code=500, detail=f"Verification failed: {str(e)}")

@app.post("/forgot-password", response_model=ForgotPasswordResponse)
def forgot_password(req: ForgotPasswordRequest, db: Session = Depends(get_db)):
    """비밀번호 찾기 - 임시 비밀번호를 이메일로 전송"""
    print(f"Forgot password request for email: {req.email}")

    try:
        # 이메일로 사용자 찾기
        user = db.execute(select(User).where(User.email == req.email)).scalar_one_or_none()
        if not user:
            raise HTTPException(status_code=404, detail="No account found with this email address")

        # 임시 비밀번호 생성
        temporary_password = generate_temporary_password()

        # 이메일 발송
        email_sent = send_account_recovery_email(req.email, user.username, temporary_password)
        if not email_sent:
            raise HTTPException(status_code=500, detail="Failed to send recovery email")

        # 사용자 비밀번호를 임시 비밀번호로 업데이트
        user.password = hash_password(temporary_password)
        db.commit()

        print(f"Temporary password sent to {req.email} for user {user.username}")

        return ForgotPasswordResponse(
            success=True,
            message="Account information sent to your email"
        )

    except HTTPException as he:
        print(f"HTTP Exception: {he.detail}")
        raise
    except Exception as e:
        print(f"Unexpected error during forgot password: {str(e)}")
        db.rollback()
        raise HTTPException(status_code=500, detail=f"Password recovery failed: {str(e)}")

@app.post("/change-password", response_model=ChangePasswordResponse)
def change_password(req: ChangePasswordRequest, db: Session = Depends(get_db)):
    """비밀번호 변경"""
    print(f"Change password request for username: {req.username}")

    try:
        # 사용자명으로 사용자 찾기
        user = db.execute(select(User).where(User.username == req.username)).scalar_one_or_none()
        if not user:
            raise HTTPException(status_code=404, detail="Username doesn't exist.")

        # 기존 비밀번호 확인
        if not verify_password(req.old_password, user.password):
            raise HTTPException(status_code=401, detail="Incorrect password.")

        # 새 비밀번호로 업데이트
        user.password = hash_password(req.new_password)
        db.commit()

        print(f"Password changed successfully for user {user.username}")

        return ChangePasswordResponse(
            success=True,
            message="Changed password successfully!"
        )

    except HTTPException as he:
        print(f"HTTP Exception: {he.detail}")
        raise
    except Exception as e:
        print(f"Unexpected error during password change: {str(e)}")
        db.rollback()
        raise HTTPException(status_code=500, detail=f"Password change failed: {str(e)}")

@app.post("/login", response_model=Token)
def login(form_data: OAuth2PasswordRequestForm = Depends(), db: Session = Depends(get_db)):
    try:
        user = authenticate_user(db, form_data.username, form_data.password)
        if not user:
            raise HTTPException(status_code=401, detail="Incorrect username or password")
        
        # 토큰 생성 (내부적으로 세션 토큰도 포함됨)
        token = create_access_token({"sub": user.username})
        
        # JWT에서 세션 토큰 추출하여 DB 업데이트 (기존 세션 무효화)
        from jose import jwt as jose_jwt
        payload = jose_jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        session_token = payload.get("session")
        
        # 사용자 세션 토큰 업데이트 (이전 로그인 무효화)
        if session_token:
            print(f"Updating session token for user {user.username}: {session_token[:10]}...")
            update_user_session(db, user, session_token)

            # 로그인 시 자동으로 온라인 상태로 설정
            user.status = "online"
            user.last_active = datetime.utcnow()
            db.commit()

            print(f"Session token updated successfully, status set to online")
        
        return Token(access_token=token)
        
    except HTTPException:
        raise  # HTTP 예외는 그대로 전파
    except Exception as e:
        print(f"Login error: {str(e)}")
        print(f"Error type: {type(e).__name__}")
        raise HTTPException(status_code=500, detail=f"Login failed: {str(e)}")

@app.get("/me", response_model=MeResponse)
def me(current_user: User = Depends(get_current_user)):
    return MeResponse(id=current_user.id, username=current_user.username, email=current_user.email, created_at=current_user.created_at)

@app.get("/user/{username}", response_model=MeResponse)
def get_user_by_username(username: str, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """사용자명으로 사용자 정보 조회"""
    print(f"Looking for user with username: '{username}'")

    # 모든 사용자 출력 (디버깅용)
    all_users = db.execute(select(User.username)).scalars().all()
    print(f"All users in database: {all_users}")

    user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
    if not user:
        print(f"User '{username}' not found in database")
        raise HTTPException(status_code=404, detail="User not found")

    print(f"Found user: {user.username}")
    return MeResponse(id=user.id, username=user.username, email=user.email, created_at=user.created_at)

@app.get("/profile", response_model=ProfileResponse)
def get_user_profile(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """사용자 프로필 정보 조회 (이름, ELO, 백분위)"""
    
    # 백분위 계산
    rapid_percentile = calculate_percentile(db, current_user.rapid_elo, "rapid_elo")
    blitz_percentile = calculate_percentile(db, current_user.blitz_elo, "blitz_elo")
    
    # 프로필 이미지 URL 생성
    profile_image_url = None
    if current_user.profile_image:
        profile_image_url = f"/profile-image/{current_user.id}"
    
    return ProfileResponse(
        username=current_user.username,
        rapid_elo=current_user.rapid_elo,
        blitz_elo=current_user.blitz_elo,
        rapid_percentile=rapid_percentile,
        blitz_percentile=blitz_percentile,
        profile_image_url=profile_image_url
    )

@app.post("/upload-profile-image")
async def upload_profile_image(
    file: UploadFile = File(...),
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db)
):
    """프로필 이미지 업로드"""
    
    # 파일 형식 확인
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Only image files are allowed")
    
    # 파일 크기 제한 (5MB)
    max_size = 5 * 1024 * 1024  # 5MB
    content = await file.read()
    if len(content) > max_size:
        raise HTTPException(status_code=400, detail="File too large. Maximum size is 5MB")
    
    # 파일 확장자 추출
    file_extension = Path(file.filename).suffix.lower()
    if file_extension not in ['.jpg', '.jpeg', '.png', '.gif']:
        raise HTTPException(status_code=400, detail="Unsupported file format")
    
    # 고유한 파일명 생성
    unique_filename = f"{current_user.id}_{uuid.uuid4()}{file_extension}"
    file_path = os.path.join(PROFILE_IMAGES_DIR, unique_filename)
    
    # 기존 프로필 이미지 삭제
    if current_user.profile_image:
        old_file_path = os.path.join(PROFILE_IMAGES_DIR, current_user.profile_image)
        if os.path.exists(old_file_path):
            os.remove(old_file_path)
    
    # 새 파일 저장
    with open(file_path, "wb") as buffer:
        buffer.write(content)
    
    # 데이터베이스 업데이트
    current_user.profile_image = unique_filename
    db.commit()
    
    return {"message": "Profile image uploaded successfully", "filename": unique_filename}

@app.get("/profile-image/{user_id}")
def get_profile_image(user_id: int, db: Session = Depends(get_db)):
    """프로필 이미지 조회"""
    
    user = db.execute(select(User).where(User.id == user_id)).scalar_one_or_none()
    if not user or not user.profile_image:
        # 기본 프로필 이미지 반환 (있다면)
        raise HTTPException(status_code=404, detail="Profile image not found")
    
    file_path = os.path.join(PROFILE_IMAGES_DIR, user.profile_image)
    if not os.path.exists(file_path):
        raise HTTPException(status_code=404, detail="Profile image file not found")
    
    return FileResponse(file_path)

# ================== 서버 상태 엔드포인트 ==================
@app.get("/status")
def get_server_status():
    """간단한 서버 상태 확인"""
    from datetime import datetime
    return {
        "server": "running",
        "timestamp": datetime.utcnow().isoformat()
    }

# ================== 친구 요청 관련 엔드포인트 ==================
@app.post("/friends/request", response_model=FriendRequestResponse)
def send_friend_request(request: AddFriendRequest, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """친구 요청 보내기"""
    try:
        # 요청 받을 사용자 찾기
        target_user = db.execute(select(User).where(User.username == request.username)).scalar_one_or_none()
        if not target_user:
            return FriendRequestResponse(message="User doesn't exist!")

        # 자기 자신에게 요청 방지
        if target_user.id == current_user.id:
            return FriendRequestResponse(message="Cannot send request to yourself!")

        # 이미 친구인지 확인
        existing_friendship = db.execute(select(Friendship).where(
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == target_user.id)) |
            ((Friendship.user_id == target_user.id) & (Friendship.friend_id == current_user.id))
        )).scalar_one_or_none()

        if existing_friendship:
            return FriendRequestResponse(message="Already friends!")

        # 이미 요청을 보냈는지 확인
        existing_request = db.execute(select(FriendRequest).where(
            (FriendRequest.sender_id == current_user.id) &
            (FriendRequest.receiver_id == target_user.id) &
            (FriendRequest.status == "pending")
        )).scalar_one_or_none()

        if existing_request:
            return FriendRequestResponse(message="Request already sent!")

        # 새 친구 요청 생성
        new_request = FriendRequest(
            sender_id=current_user.id,
            receiver_id=target_user.id,
            status="pending"
        )

        db.add(new_request)
        db.commit()
        db.refresh(new_request)

        print(f"Friend request sent from {current_user.username} to {target_user.username}")
        return FriendRequestResponse(message="Sent request!", request_id=new_request.id)

    except Exception as e:
        print(f"Error sending friend request: {e}")
        db.rollback()
        return FriendRequestResponse(message="Error sending request!")

@app.get("/friends/requests")
def get_pending_requests(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """받은 친구 요청 목록 조회"""
    try:
        # 현재 사용자가 받은 pending 상태의 친구 요청들
        requests_query = db.execute(
            select(FriendRequest, User).join(
                User, FriendRequest.sender_id == User.id
            ).where(
                (FriendRequest.receiver_id == current_user.id) &
                (FriendRequest.status == "pending")
            )
        )

        requests = requests_query.all()

        # 요청 데이터 변환
        requests_data = []
        for friend_request, sender in requests:
            request_data = FriendRequestData(
                id=friend_request.id,
                sender_username=sender.username,
                sender_rapid_elo=sender.rapid_elo,
                sender_blitz_elo=sender.blitz_elo,
                created_at=friend_request.created_at
            )
            requests_data.append(request_data)

        return {"requests": requests_data}

    except Exception as e:
        print(f"Error getting friend requests: {e}")
        raise HTTPException(status_code=500, detail="Internal server error")

@app.post("/friends/request/{request_id}/accept")
def accept_friend_request(request_id: int, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """친구 요청 수락"""
    try:
        # 요청 찾기
        friend_request = db.execute(select(FriendRequest).where(
            (FriendRequest.id == request_id) &
            (FriendRequest.receiver_id == current_user.id) &
            (FriendRequest.status == "pending")
        )).scalar_one_or_none()

        if not friend_request:
            raise HTTPException(status_code=404, detail="Friend request not found")

        # 요청자 정보 가져오기
        sender = db.execute(select(User).where(User.id == friend_request.sender_id)).scalar_one_or_none()
        if not sender:
            raise HTTPException(status_code=404, detail="Sender not found")

        # 친구 관계 생성
        new_friendship = Friendship(
            user_id=friend_request.sender_id,
            friend_id=current_user.id
        )

        # 요청 상태 업데이트
        friend_request.status = "accepted"

        db.add(new_friendship)
        db.commit()

        print(f"Friend request accepted: {sender.username} and {current_user.username} are now friends")
        return {"message": "Friend request accepted", "friend_username": sender.username}

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error accepting friend request: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Internal server error")

@app.post("/friends/request/{request_id}/reject")
def reject_friend_request(request_id: int, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """친구 요청 거절"""
    try:
        # 요청 찾기
        friend_request = db.execute(select(FriendRequest).where(
            (FriendRequest.id == request_id) &
            (FriendRequest.receiver_id == current_user.id) &
            (FriendRequest.status == "pending")
        )).scalar_one_or_none()

        if not friend_request:
            raise HTTPException(status_code=404, detail="Friend request not found")

        # 요청 상태 업데이트
        friend_request.status = "rejected"
        db.commit()

        print(f"Friend request rejected by {current_user.username}")
        return {"message": "Friend request rejected"}

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error rejecting friend request: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Internal server error")

@app.get("/friends", response_model=FriendsListResponse)
def get_friends_list(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """사용자의 친구 목록 조회"""
    try:
        # 현재 사용자의 친구들 조회 (양방향)
        friends_query = db.execute(select(User).join(
            Friendship,
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == User.id)) |
            ((Friendship.friend_id == current_user.id) & (Friendship.user_id == User.id))
        ).where(User.id != current_user.id))

        friends = friends_query.scalars().all()

        # 친구 데이터 변환
        friends_data = []
        for friend in friends:
            # 실제 상태 확인
            actual_status = determine_actual_status(friend)
            is_online = actual_status != "offline"

            friend_data = FriendData(
                username=friend.username,
                rapid_elo=friend.rapid_elo,
                blitz_elo=friend.blitz_elo,
                is_online=is_online,
                status=actual_status
            )
            friends_data.append(friend_data)

        return FriendsListResponse(friends=friends_data)

    except Exception as e:
        print(f"Error getting friends list: {e}")
        raise HTTPException(status_code=500, detail="Internal server error")

@app.delete("/friends/remove")
def remove_friend(request: AddFriendRequest, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """친구 제거"""
    try:
        # 제거할 친구 찾기
        friend_user = db.execute(select(User).where(User.username == request.username)).scalar_one_or_none()
        if not friend_user:
            raise HTTPException(status_code=404, detail="User not found")

        # 친구 관계 찾기
        friendship = db.execute(select(Friendship).where(
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == friend_user.id)) |
            ((Friendship.user_id == friend_user.id) & (Friendship.friend_id == current_user.id))
        )).scalar_one_or_none()

        if not friendship:
            raise HTTPException(status_code=404, detail="Friendship not found")

        # 친구 관계 삭제
        db.delete(friendship)
        db.commit()

        print(f"User {current_user.username} removed {friend_user.username} from friends")
        return {"message": "Friend removed successfully", "friend_username": friend_user.username}

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error removing friend: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Internal server error")

# ================== 게임 관련 함수 ==================

def determine_actual_status(user):
    """사용자의 실제 상태 확인"""
    if not user:
        return "offline"

    # DB에 저장된 상태 그대로 반환
    return user.status if user.status else "offline"

async def notify_opponent_disconnect(current_session, disconnected_user):
    """상대방에게 플레이어 연결 끊김을 알리는 함수"""
    if not current_session or not disconnected_user:
        return

    try:
        tc, gt = current_session
        if (tc not in Game_Session or gt not in Game_Session[tc]):
            return

        session = Game_Session[tc][gt]
        opponent_socket = None
        winner_name = None
        loser_name = disconnected_user

        # 연결이 끊어진 플레이어의 상대방 찾기
        if disconnected_user == session["Player1"]:
            opponent_socket = session.get("Player2_Socket")
            winner_name = session["Player2"]
        elif disconnected_user == session["Player2"]:
            opponent_socket = session.get("Player1_Socket")
            winner_name = session["Player1"]

        # 연결 끊긴 플레이어(패자)의 게임 크레딧 차감
        if loser_name:
            try:
                db_credit = SessionLocal()
                loser_user = db_credit.execute(select(User).where(User.username == loser_name)).scalar_one_or_none()
                if loser_user:
                    loser_credit = get_or_create_game_credit(db_credit, loser_user.id)
                    if loser_credit.available_games > 0:
                        loser_credit.available_games -= 1
                        db_credit.commit()
                        print(f"[Disconnect] Game credit consumed for disconnected player {loser_name}: {loser_credit.available_games} remaining")
                    else:
                        print(f"[Disconnect] No game credit to consume for {loser_name}")
                db_credit.close()
            except Exception as e:
                print(f"Error consuming game credit for disconnected player: {e}")

        # ELO 업데이트 처리
        if winner_name and loser_name:
            try:
                db = SessionLocal()
                
                # 현재 ELO 값들 가져오기
                winner_rapid_elo, winner_blitz_elo = get_user_elos(db, winner_name)
                loser_rapid_elo, loser_blitz_elo = get_user_elos(db, loser_name)
                
                # 게임 모드에 따라 적절한 ELO 사용
                if tc == "Rapid":
                    winner_current_elo = winner_rapid_elo
                    loser_current_elo = loser_rapid_elo
                elif tc == "Blitz":
                    winner_current_elo = winner_blitz_elo
                    loser_current_elo = loser_blitz_elo
                else:
                    # 기본값은 Rapid 사용
                    winner_current_elo = winner_rapid_elo
                    loser_current_elo = loser_rapid_elo
                
                # ELO 변화량 계산
                winner_new_elo, loser_new_elo = calculate_elo_change(
                    winner_current_elo, loser_current_elo
                )
                
                # 데이터베이스 업데이트
                if tc == "Rapid":
                    update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                    update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                elif tc == "Blitz":
                    update_user_elo(db, winner_name, new_blitz_elo=winner_new_elo)
                    update_user_elo(db, loser_name, new_blitz_elo=loser_new_elo)
                else:
                    # 기본값은 Rapid 업데이트
                    update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                    update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                
                print(f"ELO updated - {tc} mode (WebSocket disconnect):")
                print(f"  {winner_name}: {winner_current_elo} -> {winner_new_elo} (+{winner_new_elo - winner_current_elo})")
                print(f"  {loser_name}: {loser_current_elo} -> {loser_new_elo} ({loser_new_elo - loser_current_elo})")
                
                db.close()
                
            except Exception as e:
                print(f"Error updating ELO after WebSocket disconnect: {e}")
            
        # 상대방에게 연결 끊김 승리 메시지 전송
        if opponent_socket:
            disconnect_message = "OpponentDisconnect 0,0 0.0 Won"
            try:
                await opponent_socket.send_text(disconnect_message)
                print(f"Sent disconnect notification to opponent: {disconnect_message}")
            except Exception as e:
                print(f"Failed to send disconnect notification: {e}")
                
        # 게임 세션 정리
        try:
            del Game_Session[tc][gt]
            print(f"Cleaned up game session: {tc} {gt}")
        except:
            pass
            
    except Exception as e:
        print(f"Error in notify_opponent_disconnect: {e}")

# ================== 매치매이킹 엔드포인트 ==================

MatchmakingQueue = {}
MatchmakingQueue["Rapid"] = []
MatchmakingQueue["Blitz"] = []
Game_Session = {}
Game_Session["Rapid"] = {}
Game_Session["Blitz"] = {}
@app.websocket("/matchmaking")
async def put_matchmaking(websocket: WebSocket):
    await websocket.accept()
    username = None
    time_control = None
    db = SessionLocal()
    
    try:
        while True:
            message = await websocket.receive_text()
            time_control, token = message.strip().split()
            
            # JWT 토큰 검증
            try:
                payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
                username = payload.get("sub")
                session_token = payload.get("session")
                
                if not username or not session_token:
                    print(f"Invalid token payload for matchmaking")
                    await websocket.close(code=1008, reason="Invalid token")
                    return
                
                # 사용자 존재 및 세션 토큰 검증 (중복 로그인 방지)
                user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
                if not user:
                    print(f"User {username} not found during matchmaking")
                    await websocket.close(code=1008, reason="User not found")
                    return
                
                # 세션 토큰 검증 (중복 로그인 방지)
                if user.session_token != session_token:
                    print(f"Session mismatch for user {username} during matchmaking - disconnecting")
                    await websocket.send_text("SESSION_EXPIRED")
                    await websocket.close(code=1008, reason="Session expired")
                    return
                
                print(f"Valid session for {username} - adding to matchmaking queue")
                current_time = time.time()
                heappush(MatchmakingQueue[time_control], (current_time, username, websocket))
                await matching(time_control)
                
            except JWTError as e:
                print(f"JWT decode error during matchmaking: {e}")
                await websocket.close(code=1008, reason="Invalid token")
                return
    except WebSocketDisconnect:
        print(f"Client {username} disconnected while matchmaking.")
        # 큐에서 해당 유저 제거
        if username and time_control:
            remove_user_from_queue(username, time_control)
    except Exception as e:
        print(f"Matchmaking error for {username}: {e}")
        if username and time_control:
            remove_user_from_queue(username, time_control)
    finally:
        try:
            db.close()
        except:
            pass
        try:
            await websocket.close()
        except:
            pass

def remove_user_from_queue(username, time_control):
    """큐에서 특정 유저 제거"""
    if time_control in MatchmakingQueue:
        # heapq에서 특정 항목 제거는 복잡하므로 새 큐 생성
        new_queue = []
        while MatchmakingQueue[time_control]:
            time_stamp, user, socket = heappop(MatchmakingQueue[time_control])
            if user != username:
                heappush(new_queue, (time_stamp, user, socket))
        MatchmakingQueue[time_control] = new_queue
        print(f"Removed {username} from {time_control} queue")
    
async def matching(time_control):
    if len(MatchmakingQueue[time_control]) >= 2:
        _, player1, player1_socket = heappop(MatchmakingQueue[time_control])
        _, player2, player2_socket = heappop(MatchmakingQueue[time_control])
        game_token = str(int(time.time()))
        player_time = 0
        if time_control == "Rapid":
            player_time = 600
        else:
            player_time = 180
            
        # 데이터베이스에서 각 플레이어의 ELO 가져오기
        db = SessionLocal()
        try:
            player1_user = db.execute(select(User).where(User.username == player1)).scalar_one_or_none()
            player2_user = db.execute(select(User).where(User.username == player2)).scalar_one_or_none()
            
            # 게임 모드에 따라 적절한 ELO 사용
            if time_control == "Rapid":
                player1_elo = player1_user.rapid_elo if player1_user else 1500
                player2_elo = player2_user.rapid_elo if player2_user else 1500
            elif time_control == "Blitz":
                player1_elo = player1_user.blitz_elo if player1_user else 1500
                player2_elo = player2_user.blitz_elo if player2_user else 1500
            else:
                # 기본값은 Rapid ELO 사용
                player1_elo = player1_user.rapid_elo if player1_user else 1500
                player2_elo = player2_user.rapid_elo if player2_user else 1500
            
            print(f"Match found: {player1} ({time_control} ELO: {player1_elo}) vs {player2} ({time_control} ELO: {player2_elo})")
            
        except Exception as e:
            print(f"Error getting player ELO: {e}")
            player1_elo = 1500  # 기본값
            player2_elo = 1500  # 기본값
        finally:
            db.close()
        
        Game_Session[time_control][game_token] = {
            "Player1": player1,
            "Player2": player2,
            "Player1_Socket": player1_socket,
            "Player2_Socket": player2_socket
        }
        
        # ELO 정보를 포함한 메시지 전송: "색상 상대이름 게임토큰 상대ELO 본인ELO"
        await player1_socket.send_text(f"Red {player2} {game_token} {player2_elo} {player1_elo}")
        await player2_socket.send_text(f"Blue {player1} {game_token} {player1_elo} {player2_elo}")

# ================== 게임 세션 엔드포인트 ==================
@app.websocket("/game")
async def game(websocket: WebSocket):
    await websocket.accept()

    current_user = None
    current_session = None
    current_game_history_id = None
    move_counter = 0

    # 데이터베이스 세션 생성
    db = SessionLocal()
    
    try:
        # 첫 번째 메시지는 연결 등록용: "TimeControl GameToken UserName Connect"
        first_message = await websocket.receive_text()
        parts = first_message.strip().split()
        
        if len(parts) != 4:
            print(f"Invalid connect message format (expected 4 parts): {first_message}")
            return
            
        TimeControl, GameToken, UserName, MessageType = parts
        
        if MessageType != "Connect":
            print(f"Expected Connect message, got: {MessageType}")
            return
        
        # 게임 세션 존재 확인
        if (TimeControl not in Game_Session or 
            GameToken not in Game_Session[TimeControl]):
            print(f"Game session not found: {TimeControl}/{GameToken}")
            return
            
        session = Game_Session[TimeControl][GameToken]
        current_user = UserName
        current_session = (TimeControl, GameToken)
        
        # 게임 WebSocket 업데이트
        if UserName == session["Player1"]:
            session["Player1_Socket"] = websocket
            print(f"Player1 {UserName} connected to game {GameToken}")
        elif UserName == session["Player2"]:
            session["Player2_Socket"] = websocket
            print(f"Player2 {UserName} connected to game {GameToken}")
        else:
            print(f"Unknown user trying to join game: {UserName}")
            return

        # 게임 히스토리 생성 (두 플레이어가 모두 연결되었을 때만)
        if session.get("Player1_Socket") and session.get("Player2_Socket") and not session.get("history_created"):
            try:
                # 플레이어 ID 가져오기
                player1_user = db.execute(select(User).where(User.username == session["Player1"])).scalar_one_or_none()
                player2_user = db.execute(select(User).where(User.username == session["Player2"])).scalar_one_or_none()

                if player1_user and player2_user:
                    # GameHistory 레코드 생성
                    game_history = GameHistory(
                        game_token=GameToken,
                        player1_id=player1_user.id,
                        player2_id=player2_user.id,
                        game_mode=TimeControl.lower(),
                        game_start_time=datetime.now(),
                        player1_elo_before=player1_user.rapid_elo if TimeControl == "Rapid" else player1_user.blitz_elo,
                        player2_elo_before=player2_user.rapid_elo if TimeControl == "Rapid" else player2_user.blitz_elo
                    )

                    db.add(game_history)
                    db.commit()
                    db.refresh(game_history)

                    current_game_history_id = game_history.id
                    session["history_created"] = True
                    session["game_history_id"] = game_history.id

                    print(f"Game history created for {GameToken}: ID {game_history.id}")

            except Exception as e:
                print(f"Error creating game history: {e}")
                db.rollback()
        else:
            # 이미 생성된 게임 히스토리 ID 가져오기
            current_game_history_id = session.get("game_history_id")

        # 이후 메시지들 처리
        while True:
            message = await websocket.receive_text()
            parts = message.strip().split()

            if len(parts) < 7:
                print(f"Invalid message format: {message}")
                continue
                
            TimeControl_part, GameToken_part, UserName_part, Move_or_Wall = parts[:4]
            
            if Move_or_Wall == "Wall":
                if len(parts) >= 8:
                    # 벽의 경우: "TimeControl GameToken UserName Wall 13,9 13,11 594.6 Continue"
                    Pos = f"{parts[4]} {parts[5]}"  # "13,9 13,11"
                    Remain_Time = parts[6]          # "594.6"
                    Game_Progress = parts[7]        # "Continue"

                    # 벽 배치 기록
                    if current_game_history_id:
                        try:
                            move_counter += 1
                            current_user_obj = db.execute(select(User).where(User.username == UserName)).scalar_one_or_none()
                            if current_user_obj:
                                game_move = GameMove(
                                    game_id=current_game_history_id,
                                    move_number=move_counter,
                                    player_id=current_user_obj.id,
                                    move_type="wall",
                                    position_from=parts[4],  # "13,9"
                                    position_to=parts[5],    # "13,11"
                                    remaining_time=float(Remain_Time),
                                    move_timestamp=datetime.now()
                                )
                                db.add(game_move)
                                db.commit()
                                print(f"Wall move recorded: {parts[4]} to {parts[5]} by {UserName}")
                        except Exception as e:
                            print(f"Error recording wall move: {e}")
                            db.rollback()
                else:
                    print(f"Invalid wall message format (need 8 parts): {message}")
                    continue
            elif Move_or_Wall in ["Forfeit", "Disconnect"]:
                # 포기 또는 연결 끊김의 경우: "TimeControl GameToken UserName Forfeit/Disconnect 0,0 0.0 Lost"
                Pos = parts[4] if len(parts) > 4 else "0,0"
                Remain_Time = parts[5] if len(parts) > 5 else "0.0"
                Game_Progress = parts[6] if len(parts) > 6 else "Lost"

                # Forfeit/Disconnect 기록
                if current_game_history_id:
                    try:
                        move_counter += 1
                        current_user_obj = db.execute(select(User).where(User.username == UserName)).scalar_one_or_none()
                        if current_user_obj:
                            game_move = GameMove(
                                game_id=current_game_history_id,
                                move_number=move_counter,
                                player_id=current_user_obj.id,
                                move_type=Move_or_Wall.lower(),  # "forfeit" or "disconnect"
                                position_from=Pos,
                                remaining_time=float(Remain_Time) if Remain_Time != "0.0" else 0.0,
                                move_timestamp=datetime.now()
                            )
                            db.add(game_move)
                            db.commit()
                            print(f"{Move_or_Wall} move recorded by {UserName}")
                    except Exception as e:
                        print(f"Error recording {Move_or_Wall} move: {e}")
                        db.rollback()
                
                # 승패 결정 (포기/연결 끊김한 사람은 패자)
                loser_name = UserName
                winner_name = session["Player1"] if UserName == session["Player2"] else session["Player2"]
                
                print(f"Game ended by forfeit/disconnect - Winner: {winner_name}, Loser: {loser_name}")
                
                # ELO 업데이트 처리
                try:
                    # 현재 ELO 값들 가져오기
                    winner_rapid_elo, winner_blitz_elo = get_user_elos(db, winner_name)
                    loser_rapid_elo, loser_blitz_elo = get_user_elos(db, loser_name)
                    
                    # 게임 모드에 따라 적절한 ELO 사용
                    if TimeControl_part == "Rapid":
                        winner_current_elo = winner_rapid_elo
                        loser_current_elo = loser_rapid_elo
                    elif TimeControl_part == "Blitz":
                        winner_current_elo = winner_blitz_elo
                        loser_current_elo = loser_blitz_elo
                    else:
                        # 기본값은 Rapid 사용
                        winner_current_elo = winner_rapid_elo
                        loser_current_elo = loser_rapid_elo
                    
                    # ELO 변화량 계산
                    winner_new_elo, loser_new_elo = calculate_elo_change(
                        winner_current_elo, loser_current_elo
                    )
                    
                    # 데이터베이스 업데이트
                    if TimeControl_part == "Rapid":
                        update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                    elif TimeControl_part == "Blitz":
                        update_user_elo(db, winner_name, new_blitz_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_blitz_elo=loser_new_elo)
                    else:
                        # 기본값은 Rapid 업데이트
                        update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                    
                    print(f"ELO updated - {TimeControl_part} mode (forfeit/disconnect):")
                    print(f"  {winner_name}: {winner_current_elo} -> {winner_new_elo} (+{winner_new_elo - winner_current_elo})")
                    print(f"  {loser_name}: {loser_current_elo} -> {loser_new_elo} ({loser_new_elo - loser_current_elo})")
                    
                except Exception as e:
                    print(f"Error updating ELO after forfeit/disconnect: {e}")

                # 게임 히스토리 최종 업데이트 (forfeit/disconnect)
                if current_game_history_id:
                    try:
                        winner_user = db.execute(select(User).where(User.username == winner_name)).scalar_one_or_none()
                        loser_user = db.execute(select(User).where(User.username == loser_name)).scalar_one_or_none()

                        if winner_user and loser_user:
                            # 게임 히스토리에서 before ELO 값들 가져오기
                            current_history = db.execute(select(GameHistory).where(GameHistory.id == current_game_history_id)).scalar_one_or_none()

                            # 최종 ELO 값들 업데이트
                            winner_elo_after = winner_new_elo if 'winner_new_elo' in locals() else (winner_user.rapid_elo if TimeControl_part == "Rapid" else winner_user.blitz_elo)
                            loser_elo_after = loser_new_elo if 'loser_new_elo' in locals() else (loser_user.rapid_elo if TimeControl_part == "Rapid" else loser_user.blitz_elo)

                            # ELO 변화량 계산 (before 값들을 사용)
                            if winner_name == session["Player1"]:
                                player1_elo_change = winner_elo_after - current_history.player1_elo_before
                                player2_elo_change = loser_elo_after - current_history.player2_elo_before
                                player1_elo_after_final = winner_elo_after
                                player2_elo_after_final = loser_elo_after
                            else:
                                player1_elo_change = loser_elo_after - current_history.player1_elo_before
                                player2_elo_change = winner_elo_after - current_history.player2_elo_before
                                player1_elo_after_final = loser_elo_after
                                player2_elo_after_final = winner_elo_after

                            # GameHistory 업데이트
                            db.execute(
                                update(GameHistory)
                                .where(GameHistory.id == current_game_history_id)
                                .values(
                                    winner_id=winner_user.id,
                                    game_end_time=datetime.now(),
                                    game_result=f"{winner_name} won by {Move_or_Wall.lower()}",
                                    player1_elo_after=player1_elo_after_final,
                                    player2_elo_after=player2_elo_after_final,
                                    player1_elo_change=player1_elo_change,
                                    player2_elo_change=player2_elo_change
                                )
                            )
                            db.commit()
                            print(f"Game history finalized for forfeit/disconnect: {winner_name} wins")
                    except Exception as e:
                        print(f"Error finalizing game history for forfeit/disconnect: {e}")
                        db.rollback()

                # 상대방에게 승리 메시지 전송
                opponent_socket = session.get("Player2_Socket") if UserName == session["Player1"] else session.get("Player1_Socket")
                if opponent_socket:
                    try:
                        disconnect_message = "OpponentDisconnect 0,0 0.0 Won"
                        await opponent_socket.send_text(disconnect_message)
                        print(f"Sent forfeit/disconnect victory to opponent: {disconnect_message}")
                    except:
                        print("Failed to send forfeit/disconnect message to opponent")
                
                # 게임 세션 정리
                try:
                    del Game_Session[TimeControl_part][GameToken_part]
                    print(f"Cleaned up game session after forfeit/disconnect: {TimeControl_part} {GameToken_part}")
                except:
                    pass
                continue
                
            elif len(parts) >= 7:
                # 이동의 경우: "TimeControl GameToken UserName Move 14,8 180.0 Continue"
                Pos = parts[4]                  # "14,8"
                Remain_Time = parts[5]          # "180.0"
                Game_Progress = parts[6]        # "Continue"

                # 일반 이동 기록
                if current_game_history_id and Move_or_Wall == "Move":
                    try:
                        move_counter += 1
                        current_user_obj = db.execute(select(User).where(User.username == UserName)).scalar_one_or_none()
                        if current_user_obj:
                            game_move = GameMove(
                                game_id=current_game_history_id,
                                move_number=move_counter,
                                player_id=current_user_obj.id,
                                move_type="move",
                                position_from=Pos,  # 새로운 위치
                                remaining_time=float(Remain_Time),
                                move_timestamp=datetime.now()
                            )
                            db.add(game_move)
                            db.commit()
                            print(f"Move recorded: {Pos} by {UserName}")
                    except Exception as e:
                        print(f"Error recording move: {e}")
                        db.rollback()
            else:
                print(f"Invalid message format: {message}")
                continue
            
            if Game_Progress in ["Won", "Lost", "Win", "Lose"]:
                # 승패 결정 및 Elo 업데이트
                winner_name = None
                loser_name = None
                
                if Game_Progress in ["Won", "Win"]:
                    winner_name = UserName
                    loser_name = session["Player1"] if UserName == session["Player2"] else session["Player2"]
                elif Game_Progress in ["Lost", "Lose"]:
                    loser_name = UserName
                    winner_name = session["Player1"] if UserName == session["Player2"] else session["Player2"]
                
                print(f"Game ended - Winner: {winner_name}, Loser: {loser_name}")
                
                # ELO 업데이트 처리
                try:
                    # 현재 ELO 값들 가져오기
                    winner_rapid_elo, winner_blitz_elo = get_user_elos(db, winner_name)
                    loser_rapid_elo, loser_blitz_elo = get_user_elos(db, loser_name)
                    
                    # 게임 모드에 따라 적절한 ELO 사용
                    if TimeControl_part == "Rapid":
                        winner_current_elo = winner_rapid_elo
                        loser_current_elo = loser_rapid_elo
                    elif TimeControl_part == "Blitz":
                        winner_current_elo = winner_blitz_elo
                        loser_current_elo = loser_blitz_elo
                    else:
                        # 기본값은 Rapid 사용
                        winner_current_elo = winner_rapid_elo
                        loser_current_elo = loser_rapid_elo
                    
                    # ELO 변화량 계산
                    winner_new_elo, loser_new_elo = calculate_elo_change(
                        winner_current_elo, loser_current_elo
                    )
                    
                    # 데이터베이스 업데이트
                    if TimeControl_part == "Rapid":
                        update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                    elif TimeControl_part == "Blitz":
                        update_user_elo(db, winner_name, new_blitz_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_blitz_elo=loser_new_elo)
                    else:
                        # 기본값은 Rapid 업데이트
                        update_user_elo(db, winner_name, new_rapid_elo=winner_new_elo)
                        update_user_elo(db, loser_name, new_rapid_elo=loser_new_elo)
                    
                    print(f"ELO updated - {TimeControl_part} mode:")
                    print(f"  {winner_name}: {winner_current_elo} -> {winner_new_elo} (+{winner_new_elo - winner_current_elo})")
                    print(f"  {loser_name}: {loser_current_elo} -> {loser_new_elo} ({loser_new_elo - loser_current_elo})")
                    
                except Exception as e:
                    print(f"Error updating ELO: {e}")

                # 게임 히스토리 최종 업데이트 (정상 게임 종료)
                if current_game_history_id:
                    try:
                        winner_user = db.execute(select(User).where(User.username == winner_name)).scalar_one_or_none()
                        loser_user = db.execute(select(User).where(User.username == loser_name)).scalar_one_or_none()

                        if winner_user and loser_user:
                            # 게임 히스토리에서 before ELO 값들 가져오기
                            current_history = db.execute(select(GameHistory).where(GameHistory.id == current_game_history_id)).scalar_one_or_none()

                            # 최종 ELO 값들 업데이트
                            winner_elo_after = winner_new_elo if 'winner_new_elo' in locals() else (winner_user.rapid_elo if TimeControl_part == "Rapid" else winner_user.blitz_elo)
                            loser_elo_after = loser_new_elo if 'loser_new_elo' in locals() else (loser_user.rapid_elo if TimeControl_part == "Rapid" else loser_user.blitz_elo)

                            # ELO 변화량 계산 (before 값들을 사용)
                            if winner_name == session["Player1"]:
                                player1_elo_change = winner_elo_after - current_history.player1_elo_before
                                player2_elo_change = loser_elo_after - current_history.player2_elo_before
                                player1_elo_after_final = winner_elo_after
                                player2_elo_after_final = loser_elo_after
                            else:
                                player1_elo_change = loser_elo_after - current_history.player1_elo_before
                                player2_elo_change = winner_elo_after - current_history.player2_elo_before
                                player1_elo_after_final = loser_elo_after
                                player2_elo_after_final = winner_elo_after

                            # GameHistory 업데이트
                            db.execute(
                                update(GameHistory)
                                .where(GameHistory.id == current_game_history_id)
                                .values(
                                    winner_id=winner_user.id,
                                    game_end_time=datetime.now(),
                                    game_result=f"{winner_name} won normally",
                                    player1_elo_after=player1_elo_after_final,
                                    player2_elo_after=player2_elo_after_final,
                                    player1_elo_change=player1_elo_change,
                                    player2_elo_change=player2_elo_change
                                )
                            )
                            db.commit()
                            print(f"Game history finalized for normal game end: {winner_name} wins")
                    except Exception as e:
                        print(f"Error finalizing game history for normal game end: {e}")
                        db.rollback()

                # 상대방에게 게임 종료 메시지 전송 (실제 위치 포함)
                if UserName == session["Player1"]:
                    target_socket = session["Player2_Socket"]
                    opponent_result = "Lost" if Game_Progress in ["Won", "Win"] else "Won"
                else:
                    target_socket = session["Player1_Socket"]
                    opponent_result = "Lost" if Game_Progress in ["Won", "Win"] else "Won"

                if target_socket:
                    try:
                        # 실제 위치와 시간 정보를 포함해서 전송
                        await target_socket.send_text(f"GameEnd {Pos} {Remain_Time} {opponent_result}")
                        print(f"Sent game end message to opponent: {opponent_result} at position {Pos}")
                    except:
                        print(f"Failed to send game end message to opponent")
                
                # 게임 세션 정리
                if GameToken in Game_Session[TimeControl]:
                    del Game_Session[TimeControl][GameToken]
                    print(f"Game session {GameToken} ended and cleaned up")
                
                await websocket.close()
                break
            else:
                # 상대방에게 메시지 전달
                if UserName == session["Player1"]:
                    target_socket = session["Player2_Socket"]
                    if target_socket:
                        try:
                            await target_socket.send_text(f"{Move_or_Wall} {Pos} {Remain_Time} {Game_Progress}")
                            print(f"Forwarded message from Player1 to Player2: {Move_or_Wall} {Pos}")
                        except:
                            print(f"Player2 socket is closed, cannot forward message")
                elif UserName == session["Player2"]:
                    target_socket = session["Player1_Socket"]
                    if target_socket:
                        try:
                            await target_socket.send_text(f"{Move_or_Wall} {Pos} {Remain_Time} {Game_Progress}")
                            print(f"Forwarded message from Player2 to Player1: {Move_or_Wall} {Pos}")
                        except:
                            print(f"Player1 socket is closed, cannot forward message")
                    
    except WebSocketDisconnect:
        print(f"Game WebSocket disconnected: {current_user}")
        # 상대방에게 연결 끊김 알림
        await notify_opponent_disconnect(current_session, current_user)
    except Exception as e:
        print(f"Game WebSocket error: {e}")
        # 상대방에게 연결 끊김 알림
        await notify_opponent_disconnect(current_session, current_user)
    finally:
        # 연결 해제 시 세션에서 WebSocket 정리
        if current_session and current_user:
            try:
                tc, gt = current_session
                if (tc in Game_Session and gt in Game_Session[tc]):
                    session = Game_Session[tc][gt]
                    if current_user == session["Player1"]:
                        session["Player1_Socket"] = None
                    elif current_user == session["Player2"]:
                        session["Player2_Socket"] = None
            except:
                pass
                
        try:
            await websocket.close()
        except:
            pass
        
        # 데이터베이스 세션 정리
        try:
            db.close()
        except:
            pass


# ================== 채팅 엔드포인트 ==================

@app.post("/chat/send", response_model=ChatMessageResponse)
def send_chat_message(message_data: ChatMessageRequest, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """채팅 메시지 전송"""
    try:
        # 수신자 찾기
        receiver = db.execute(select(User).where(User.username == message_data.receiver_username)).scalar_one_or_none()
        if not receiver:
            raise HTTPException(status_code=404, detail="Receiver not found")

        # 친구 관계 확인
        friendship_exists = db.execute(select(Friendship).where(
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == receiver.id)) |
            ((Friendship.user_id == receiver.id) & (Friendship.friend_id == current_user.id))
        )).scalar_one_or_none()

        if not friendship_exists:
            raise HTTPException(status_code=403, detail="You can only send messages to friends")

        # 메시지 저장
        chat_message = ChatMessage(
            sender_id=current_user.id,
            receiver_id=receiver.id,
            message=message_data.message,
            is_read=False  # 명시적으로 읽지 않음으로 설정
        )

        db.add(chat_message)
        db.commit()

        print(f"Chat message sent from {current_user.username} to {receiver.username}")
        return ChatMessageResponse(success=True, message="Message sent successfully")

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error sending chat message: {e}")
        raise HTTPException(status_code=500, detail="Failed to send message")

@app.get("/chat/history/{friend_username}", response_model=ChatHistoryResponse)
def get_chat_history(friend_username: str, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """친구와의 채팅 기록 조회"""
    try:
        # 친구 찾기
        friend = db.execute(select(User).where(User.username == friend_username)).scalar_one_or_none()
        if not friend:
            raise HTTPException(status_code=404, detail="Friend not found")

        # 친구 관계 확인
        friendship_exists = db.execute(select(Friendship).where(
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == friend.id)) |
            ((Friendship.user_id == friend.id) & (Friendship.friend_id == current_user.id))
        )).scalar_one_or_none()

        if not friendship_exists:
            raise HTTPException(status_code=403, detail="You can only view messages with friends")

        # 채팅 기록 조회 (최근 50개 메시지)
        messages = db.execute(select(ChatMessage).where(
            ((ChatMessage.sender_id == current_user.id) & (ChatMessage.receiver_id == friend.id)) |
            ((ChatMessage.sender_id == friend.id) & (ChatMessage.receiver_id == current_user.id))
        ).order_by(ChatMessage.created_at.desc()).limit(50)).scalars().all()

        # 메시지를 시간 순으로 정렬 (오래된 것부터)
        messages = list(reversed(messages))

        # 해당 친구로부터 받은 읽지 않은 메시지들을 모두 읽음으로 표시
        db.execute(
            update(ChatMessage)
            .where(
                (ChatMessage.sender_id == friend.id) &
                (ChatMessage.receiver_id == current_user.id) &
                (ChatMessage.is_read == False)
            )
            .values(is_read=True)
        )
        db.commit()

        # 응답 데이터 구성
        message_list = []
        for msg in messages:
            is_sent_by_me = msg.sender_id == current_user.id
            sender_username = current_user.username if is_sent_by_me else friend.username
            receiver_username = friend.username if is_sent_by_me else current_user.username

            message_list.append(ChatMessageData(
                id=msg.id,
                sender_username=sender_username,
                receiver_username=receiver_username,
                message=msg.message,
                created_at=msg.created_at,
                is_sent_by_me=is_sent_by_me
            ))

        return ChatHistoryResponse(messages=message_list)

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error getting chat history: {e}")
        raise HTTPException(status_code=500, detail="Failed to get chat history")

@app.get("/messages/unread-counts", response_model=UnreadMessagesResponse)
def get_unread_message_counts(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """각 친구별 읽지 않은 메시지 개수 조회"""
    try:
        # 현재 사용자의 친구 목록 조회
        friends_query = select(User).join(Friendship,
            (Friendship.user_id == current_user.id) & (Friendship.friend_id == User.id) |
            (Friendship.friend_id == current_user.id) & (Friendship.user_id == User.id)
        ).where(User.id != current_user.id)

        friends = db.execute(friends_query).scalars().all()
        print(f"[UNREAD_MESSAGES] Found {len(friends)} friends for user {current_user.username}")

        unread_data = []

        for friend in friends:
            # 해당 친구로부터 받은 읽지 않은 메시지 개수 조회
            unread_count = db.execute(select(func.count(ChatMessage.id)).where(
                (ChatMessage.sender_id == friend.id) &
                (ChatMessage.receiver_id == current_user.id) &
                (ChatMessage.is_read == False)
            )).scalar()

            print(f"[UNREAD_MESSAGES] {friend.username} -> {current_user.username}: {unread_count} unread messages")

            # 읽지 않은 메시지가 있는 친구만 응답에 포함
            if unread_count > 0:
                unread_data.append(UnreadMessageData(
                    friend_username=friend.username,
                    unread_count=unread_count
                ))
                print(f"[UNREAD_MESSAGES] Added {friend.username} to response with {unread_count} unread messages")

        response = UnreadMessagesResponse(unread_messages=unread_data)
        print(f"[UNREAD_MESSAGES] Returning {len(unread_data)} friends with unread messages for {current_user.username}")
        return response

    except Exception as e:
        print(f"Error getting unread message counts: {e}")
        raise HTTPException(status_code=500, detail="Failed to get unread message counts")

@app.post("/messages/mark-read/{friend_username}")
def mark_messages_as_read(friend_username: str, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """특정 친구로부터 받은 메시지들을 모두 읽음으로 표시"""
    try:
        # 친구 찾기
        friend = db.execute(select(User).where(User.username == friend_username)).scalar_one_or_none()
        if not friend:
            raise HTTPException(status_code=404, detail="Friend not found")

        # 친구 관계 확인
        friendship_exists = db.execute(select(Friendship).where(
            ((Friendship.user_id == current_user.id) & (Friendship.friend_id == friend.id)) |
            ((Friendship.friend_id == current_user.id) & (Friendship.user_id == friend.id))
        )).scalar_one_or_none()

        if not friendship_exists:
            raise HTTPException(status_code=403, detail="You can only mark messages from friends as read")

        # 해당 친구로부터 받은 읽지 않은 메시지들을 모두 읽음으로 표시
        db.execute(
            update(ChatMessage)
            .where(
                (ChatMessage.sender_id == friend.id) &
                (ChatMessage.receiver_id == current_user.id) &
                (ChatMessage.is_read == False)
            )
            .values(is_read=True)
        )

        db.commit()

        print(f"Marked messages from {friend_username} as read for {current_user.username}")
        return {"message": f"Marked messages from {friend_username} as read"}

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error marking messages as read: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Failed to mark messages as read")

# ================== 사용자 상태 API ==================
@app.post("/status", response_model=UserStatusResponse)
def update_status(request: UserStatusRequest, db: Session = Depends(get_db)):
    """사용자 상태 업데이트"""
    try:
        # 유효한 상태 값 확인
        valid_statuses = ["online", "offline", "in_game"]
        if request.status not in valid_statuses:
            raise HTTPException(
                status_code=400,
                detail=f"Invalid status. Must be one of: {valid_statuses}"
            )

        # 사용자 조회
        user = db.execute(select(User).where(User.username == request.username)).scalar_one_or_none()
        if not user:
            raise HTTPException(status_code=404, detail="User not found")

        # 상태 업데이트
        user.status = request.status
        user.last_active = datetime.utcnow()
        db.commit()

        print(f"User {request.username} status updated to: {request.status}")

        return UserStatusResponse(
            username=user.username,
            status=user.status
        )

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error updating user status: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Failed to update status")
    
# ================== 친구 대전 API ==================
fight_sockets = {}
# 임시: 모든 사용자 상태를 offline으로 초기화하는 엔드포인트
@app.post("/reset-all-status")
def reset_all_user_status(db: Session = Depends(get_db)):
    try:
        # 모든 사용자의 status를 offline으로 설정
        db.execute(
            "UPDATE users SET status = 'offline'"
        )
        db.commit()

        updated_count = db.execute(
            "SELECT COUNT(*) FROM users WHERE status = 'offline'"
        ).scalar()

        return {"message": f"All user status reset to offline. Updated {updated_count} users."}
    except Exception as e:
        db.rollback()
        print(f"Error resetting user status: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to reset status: {str(e)}")

# 특정 사용자의 status 수정 엔드포인트
@app.post("/update-user-status/{username}")
def update_user_status_manual(username: str, status: str, db: Session = Depends(get_db)):
    try:
        user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
        if not user:
            raise HTTPException(status_code=404, detail="User not found")

        old_status = user.status
        user.status = status
        db.commit()

        print(f"Updated {username} status from '{old_status}' to '{status}'")
        return {"message": f"Updated {username} status to {status}"}
    except HTTPException:
        raise
    except Exception as e:
        db.rollback()
        print(f"Error updating user status: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to update status: {str(e)}")

# ================== 게임 히스토리 API ==================
@app.get("/game-history", response_model=GameHistoryResponse)
def get_game_history(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """현재 사용자의 게임 히스토리 조회"""
    try:
        # 현재 사용자가 참여한 게임 히스토리 조회 (최신순)
        game_histories = db.execute(
            select(GameHistory)
            .where(
                (GameHistory.player1_id == current_user.id) |
                (GameHistory.player2_id == current_user.id)
            )
            .order_by(GameHistory.game_start_time.desc())
        ).scalars().all()

        # 응답 데이터 구성
        history_list = []
        for history in game_histories:
            # 상대방 정보 찾기
            if history.player1_id == current_user.id:
                opponent_id = history.player2_id
                my_elo_before = history.player1_elo_before
                my_elo_after = history.player1_elo_after
                my_elo_change = history.player1_elo_change
            else:
                opponent_id = history.player1_id
                my_elo_before = history.player2_elo_before
                my_elo_after = history.player2_elo_after
                my_elo_change = history.player2_elo_change

            # 상대방 사용자 정보 가져오기
            opponent = db.execute(select(User).where(User.id == opponent_id)).scalar_one_or_none()
            opponent_username = opponent.username if opponent else "Unknown"

            # 승패 결정
            is_winner = history.winner_id == current_user.id
            result = "Win" if is_winner else "Lose"

            history_list.append(GameHistoryData(
                id=history.id,
                game_token=history.game_token,
                opponent_username=opponent_username,
                game_mode=history.game_mode,
                result=result,
                my_elo_before=my_elo_before,
                my_elo_after=my_elo_after,
                my_elo_change=my_elo_change,
                game_start_time=str(history.game_start_time) if history.game_start_time else "",
                game_end_time=str(history.game_end_time) if history.game_end_time else None,
                game_result=history.game_result
            ))

        return GameHistoryResponse(game_history=history_list)

    except Exception as e:
        print(f"Error getting game history: {e}")
        raise HTTPException(status_code=500, detail="Failed to get game history")

@app.get("/game-history/{game_id}/moves", response_model=GameDetailResponse)
def get_game_moves(game_id: int, current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """특정 게임의 상세 수 정보 조회"""
    try:
        # 게임 히스토리 확인 (현재 사용자가 참여한 게임인지 검증)
        game_history = db.execute(
            select(GameHistory)
            .where(
                (GameHistory.id == game_id) &
                ((GameHistory.player1_id == current_user.id) | (GameHistory.player2_id == current_user.id))
            )
        ).scalar_one_or_none()

        if not game_history:
            raise HTTPException(status_code=404, detail="Game not found or access denied")

        # 해당 게임의 모든 수 조회 (순서대로)
        game_moves = db.execute(
            select(GameMove)
            .where(GameMove.game_id == game_id)
            .order_by(GameMove.move_number.asc())
        ).scalars().all()

        # 플레이어 정보 가져오기
        player1 = db.execute(select(User).where(User.id == game_history.player1_id)).scalar_one_or_none()
        player2 = db.execute(select(User).where(User.id == game_history.player2_id)).scalar_one_or_none()

        # 응답 데이터 구성
        move_list = []
        for move in game_moves:
            # 플레이어 사용자명 찾기
            player_username = "Unknown"
            if move.player_id == game_history.player1_id and player1:
                player_username = player1.username
            elif move.player_id == game_history.player2_id and player2:
                player_username = player2.username

            move_list.append(GameMoveData(
                move_number=move.move_number,
                player_username=player_username,
                move_type=move.move_type,
                position_from=move.position_from,
                position_to=move.position_to,
                remaining_time=move.remaining_time,
                move_timestamp=str(move.move_timestamp) if move.move_timestamp else ""
            ))

        return GameDetailResponse(
            game_id=game_history.id,
            game_token=game_history.game_token,
            player1_username=player1.username if player1 else "Unknown",
            player2_username=player2.username if player2 else "Unknown",
            game_mode=game_history.game_mode,
            game_start_time=str(game_history.game_start_time) if game_history.game_start_time else "",
            game_end_time=str(game_history.game_end_time) if game_history.game_end_time else None,
            game_result=game_history.game_result,
            moves=move_list
        )

    except HTTPException:
        raise
    except Exception as e:
        print(f"Error getting game moves: {e}")
        raise HTTPException(status_code=500, detail="Failed to get game moves")

# ================== 디버그/테스트 API ==================
@app.get("/debug/database-info")
def get_database_info(db: Session = Depends(get_db)):
    """데이터베이스 정보 확인 (테스트용)"""
    try:
        # GameHistory 테이블 존재 확인
        game_history_count = db.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='game_histories'").scalar()
        game_move_count = db.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='game_moves'").scalar()

        # 각 테이블의 레코드 수
        total_games = 0
        total_moves = 0

        if game_history_count > 0:
            total_games = db.execute("SELECT COUNT(*) FROM game_histories").scalar()

        if game_move_count > 0:
            total_moves = db.execute("SELECT COUNT(*) FROM game_moves").scalar()

        return {
            "database_tables": {
                "game_histories_exists": game_history_count > 0,
                "game_moves_exists": game_move_count > 0
            },
            "record_counts": {
                "total_games": total_games,
                "total_moves": total_moves
            },
            "server_status": "Game history system is active"
        }
    except Exception as e:
        return {
            "error": str(e),
            "server_status": "Game history system may not be properly configured"
        }

@app.get("/debug/recent-games")
def get_recent_games_debug(db: Session = Depends(get_db)):
    """최근 게임들 확인 (테스트용)"""
    try:
        games = db.execute(
            select(GameHistory)
            .order_by(GameHistory.game_start_time.desc())
            .limit(5)
        ).scalars().all()

        game_list = []
        for game in games:
            player1 = db.execute(select(User).where(User.id == game.player1_id)).scalar_one_or_none()
            player2 = db.execute(select(User).where(User.id == game.player2_id)).scalar_one_or_none()

            game_list.append({
                "id": game.id,
                "game_token": game.game_token,
                "player1": player1.username if player1 else "Unknown",
                "player2": player2.username if player2 else "Unknown",
                "game_mode": game.game_mode,
                "game_start_time": str(game.game_start_time),
                "game_end_time": str(game.game_end_time) if game.game_end_time else None,
                "winner_id": game.winner_id
            })

        return {
            "recent_games": game_list,
            "total_found": len(game_list)
        }
    except Exception as e:
        return {
            "error": str(e),
            "message": "Failed to retrieve recent games"
        }

@app.websocket("/fight")
async def fight(websocket: WebSocket):
    db = SessionLocal()
    username = None
    await websocket.accept()
    try:
        while True:
            message = await websocket.receive_text()
            print(f"[FIGHT] SERVER RECEIVED: {message}")

            parts = message.strip().split()
            if len(parts) < 2:
                print(f"[FIGHT] ERROR: Invalid message format: {message}")
                continue

            first = parts[0]
            second = parts[1] if len(parts) > 1 else ""
            third = parts[2] if len(parts) > 2 else ""

            if first == "start": #형식: start username dummy_text
                username = second
                fight_sockets[username] = websocket
                print(f"[FIGHT] User {username} connected to fight socket")

            elif first == "rapid" or first == "blitz": #형식: rapid_or_blitz sender_name friend_name
                print(f"[FIGHT] Processing {first} request from {second} to {third}")

                user = db.execute(select(User).where(User.username == third)).scalar_one_or_none()
                if not user:
                    response = f"{third} not_found {first}"
                    print(f"[FIGHT] SERVER SENDING: {response}")
                    await websocket.send_text(response)
                    continue

                status = user.status
                response = f"{third} {status} {first}"
                print(f"[FIGHT] SERVER SENDING: {response}")
                await websocket.send_text(response)

                if status == "online":
                    if third in fight_sockets:
                        friend_socket = fight_sockets[third]
                        fight_message = f"fight {second} {first}"
                        print(f"[FIGHT] SERVER SENDING to {third}: {fight_message}")
                        await friend_socket.send_text(fight_message)
                    else:
                        print(f"[FIGHT] ERROR: {third} not connected to fight socket")

            elif first == "accept": #형식: accept opponent time_control
                print(f"[FIGHT] Accept request: {message}")
                opponent_name = second
                time_control = third

                # 1. 게임 토큰 생성
                game_token = str(int(time.time()))

                # 2. 게임 토큰이 짝수인지 홀수인지에 따라 플레이어 색깔 결정
                token_number = int(game_token)
                if token_number % 2 == 0:
                    # 짝수: 수락한 사람이 Red, 요청한 사람이 Blue
                    accepter_color = "Red"
                    requester_color = "Blue"
                else:
                    # 홀수: 수락한 사람이 Blue, 요청한 사람이 Red
                    accepter_color = "Blue"
                    requester_color = "Red"

                print(f"[FIGHT] Game created - Token: {game_token}, {username}({accepter_color}) vs {opponent_name}({requester_color})")

                # 3. 각 플레이어의 ELO 가져오기
                try:
                    accepter_user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
                    requester_user = db.execute(select(User).where(User.username == opponent_name)).scalar_one_or_none()

                    if time_control.lower() == "rapid":
                        accepter_elo = accepter_user.rapid_elo if accepter_user else 1500
                        requester_elo = requester_user.rapid_elo if requester_user else 1500
                        time_control_key = "Rapid"
                    elif time_control.lower() == "blitz":
                        accepter_elo = accepter_user.blitz_elo if accepter_user else 1500
                        requester_elo = requester_user.blitz_elo if requester_user else 1500
                        time_control_key = "Blitz"
                    else:
                        accepter_elo = accepter_user.rapid_elo if accepter_user else 1500
                        requester_elo = requester_user.rapid_elo if requester_user else 1500
                        time_control_key = "Rapid"

                    print(f"[FIGHT] ELO - {username}: {accepter_elo}, {opponent_name}: {requester_elo}")

                except Exception as e:
                    print(f"[FIGHT] Error getting ELO: {e}")
                    accepter_elo = 1500
                    requester_elo = 1500
                    time_control_key = "Rapid"

                # 4. Game_Session에 게임 생성
                if accepter_color == "Red":
                    player1 = username
                    player2 = opponent_name
                    player1_socket = websocket
                    player2_socket = fight_sockets.get(opponent_name)
                else:
                    player1 = opponent_name
                    player2 = username
                    player1_socket = fight_sockets.get(opponent_name)
                    player2_socket = websocket

                Game_Session[time_control_key][game_token] = {
                    "Player1": player1,
                    "Player2": player2,
                    "Player1_Socket": player1_socket,
                    "Player2_Socket": player2_socket
                }

                # 5. 클라이언트들에게 게임 생성 메시지 전송
                # 수락한 사람에게 메시지 (본인 색깔, 상대 이름, 게임 토큰, 상대 ELO, 본인 ELO, 게임 모드)
                accepter_message = f"{accepter_color} {opponent_name} {game_token} {requester_elo} {accepter_elo} {time_control_key}"
                await websocket.send_text(accepter_message)
                print(f"[FIGHT] Sent to accepter {username}: {accepter_message}")

                # 요청한 사람에게 메시지
                if opponent_name in fight_sockets:
                    requester_message = f"{requester_color} {username} {game_token} {accepter_elo} {requester_elo} {time_control_key}"
                    await fight_sockets[opponent_name].send_text(requester_message)
                    print(f"[FIGHT] Sent to requester {opponent_name}: {requester_message}")
                else:
                    print(f"[FIGHT] ERROR: {opponent_name} not connected to fight socket")

            elif first == "decline": #형식: decline opponent time_control
                print(f"[FIGHT] Decline request: {message}")
                #send opponent decline message (decline username rapid_or_blitz)
                pass

            else:
                print(f"[FIGHT] Unknown command: {first}")

    except WebSocketDisconnect:
        if username and username in fight_sockets:
            del fight_sockets[username]
            print(f"[FIGHT] User {username} disconnected from fight socket")
    except Exception as e:
        print(f"[FIGHT] ERROR: {e}")
        if username and username in fight_sockets:
            del fight_sockets[username]


# ================== AI 게임 WebSocket ==================
# AI 세션 저장소 (클라이언트별 AI 인스턴스)
ai_sessions = {}

@app.websocket("/ai-game")
async def ai_game(websocket: WebSocket):
    """AI와의 대전 WebSocket 엔드포인트"""
    await websocket.accept()
    ai = None

    try:
        # 첫 메시지: "PlayerColor Difficulty"
        initial_message = await websocket.receive_text()
        print(f"[AI Game] Received initial message: {initial_message}")

        parts = initial_message.strip().split()
        if len(parts) != 2:
            await websocket.send_text("Error: Invalid initial message format")
            await websocket.close()
            return

        player_color = parts[0].lower()  # "red" or "blue"
        difficulty = parts[1].lower()     # "easy", "medium", "hard"

        # AI 플레이어 색상 결정 (사용자의 반대편)
        ai_player = 'blue' if player_color == 'red' else 'red'

        print(f"[AI Game] Player: {player_color}, AI: {ai_player}, Difficulty: {difficulty}")

        # AI 인스턴스 생성
        ai = QuoridorAI(player=ai_player, difficulty=difficulty)

        # AI가 Red(먼저 시작)인 경우 첫 수를 먼저 보냄
        if ai_player == 'red':
            print(f"[AI Game] AI (Red) starts first")
            best_move = ai.get_best_move()

            if best_move is not None:
                if best_move['type'] == 'move':
                    # 17x17 좌표계 사용 (변환 불필요)
                    response = f"Move {best_move['position']}"
                    await websocket.send_text(response)
                    print(f"[AI Game] Sent AI first move: {best_move['position']}")
                elif best_move['type'] == 'wall':
                    # 17x17 좌표계 사용 (변환 불필요)
                    response = f"Wall {best_move['wall_type']} {best_move['position']}"
                    await websocket.send_text(response)
                    print(f"[AI Game] Sent AI first wall: {best_move['wall_type']} at {best_move['position']}")

        # 게임 루프
        while True:
            # 클라이언트로부터 메시지 수신
            message = await websocket.receive_text()
            print(f"[AI Game] Received from client: {message}")

            # 메시지 파싱
            if message.startswith("wall:"):
                # 벽 배치: "wall:horizontal:Y:X" 또는 "wall:vertical:Y:X"
                # 17x17 좌표계 사용 (변환 불필요)
                print(f"[AI Game] Received wall placement: {message}")
                success = ai.apply_opponent_move(message)

                if not success:
                    print(f"[AI Game] Failed to apply opponent wall")
                    continue

            else:
                # 이동: "Y,X"
                # 17x17 좌표계 사용 (변환 불필요)
                print(f"[AI Game] Received move: {message}")
                success = ai.apply_opponent_move(message)

                if not success:
                    print(f"[AI Game] Failed to apply opponent move")
                    continue

            # 게임 종료 확인 (플레이어가 이겼는지)
            if ai.is_game_over():
                winner = ai.get_winner()
                await websocket.send_text(f"GameEnd {winner}")
                print(f"[AI Game] Game ended. Winner: {winner}")
                break

            # AI의 수 계산
            best_move = ai.get_best_move()

            if best_move is None:
                print(f"[AI Game] AI has no valid move")
                break

            # AI의 수를 클라이언트에 전송 (17x17 좌표계, 변환 불필요)
            if best_move['type'] == 'move':
                response = f"Move {best_move['position']}"
                await websocket.send_text(response)
                print(f"[AI Game] Sent AI move: {best_move['position']}")

            elif best_move['type'] == 'wall':
                response = f"Wall {best_move['wall_type']} {best_move['position']}"
                await websocket.send_text(response)
                print(f"[AI Game] Sent AI wall: {best_move['wall_type']} at {best_move['position']}")

            # 현재 배치된 모든 벽 출력 (x,y 순서)
            print(f"[WALL_STATE] === Current Walls on Board ===")
            if ai.state.horizontal_walls:
                print(f"[WALL_STATE] Horizontal walls (x,y):")
                for y, x in sorted(ai.state.horizontal_walls):
                    print(f"[WALL_STATE]   - ({x},{y})")
            else:
                print(f"[WALL_STATE] Horizontal walls: None")

            if ai.state.vertical_walls:
                print(f"[WALL_STATE] Vertical walls (x,y):")
                for y, x in sorted(ai.state.vertical_walls):
                    print(f"[WALL_STATE]   - ({x},{y})")
            else:
                print(f"[WALL_STATE] Vertical walls: None")

            # 게임 종료 확인 (AI가 이겼는지)
            if ai.is_game_over():
                winner = ai.get_winner()
                await websocket.send_text(f"GameEnd {winner}")
                print(f"[AI Game] Game ended. Winner: {winner}")
                break

    except WebSocketDisconnect:
        print(f"[AI Game] Client disconnected")
    except Exception as e:
        print(f"[AI Game] Error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        # AI 인스턴스 정리
        if ai:
            del ai

# ================== 게임 크레딧 API ==================

def get_or_create_game_credit(db: Session, user_id: int) -> GameCredit:
    """사용자의 게임 크레딧 가져오기 또는 생성"""
    credit = db.execute(select(GameCredit).where(GameCredit.user_id == user_id)).scalar_one_or_none()

    if not credit:
        # 새로운 크레딧 생성
        credit = GameCredit(
            user_id=user_id,
            available_games=5,
            last_reset_date=datetime.utcnow()
        )
        db.add(credit)
        db.commit()
        db.refresh(credit)

    return credit

def check_and_reset_daily(db: Session, credit: GameCredit) -> GameCredit:
    """일일 리셋 체크 및 실행"""
    today = datetime.utcnow().date()
    last_reset_date = credit.last_reset_date.date()

    if today > last_reset_date:
        # 날짜가 바뀌면 최소 5회 보장 (광고로 모은 횟수는 유지)
        credit.available_games = max(credit.available_games, 5)
        credit.last_reset_date = datetime.utcnow()
        db.commit()
        db.refresh(credit)

    return credit

@app.get("/api/game-credits", response_model=GameCreditResponse)
def get_game_credits(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """
    게임 크레딧 조회
    - 사용 가능한 게임 횟수 반환
    """
    try:
        credit = get_or_create_game_credit(db, current_user.id)
        credit = check_and_reset_daily(db, credit)

        can_play = credit.available_games > 0

        return GameCreditResponse(
            available_games=credit.available_games,
            can_play=can_play,
            last_reset_date=credit.last_reset_date
        )
    except Exception as e:
        print(f"Error getting game credits: {e}")
        raise HTTPException(status_code=500, detail="Failed to get game credits")

@app.post("/api/game-credits/consume", response_model=ConsumeGameResponse)
def consume_game_credit(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """
    게임 1회 소비
    - 게임 횟수 1 차감
    """
    try:
        credit = get_or_create_game_credit(db, current_user.id)
        credit = check_and_reset_daily(db, credit)

        # 게임 횟수 체크
        if credit.available_games <= 0:
            return ConsumeGameResponse(
                success=False,
                remaining_games=0,
                message="No games available"
            )

        # 게임 1회 차감
        credit.available_games -= 1
        db.commit()
        db.refresh(credit)

        return ConsumeGameResponse(
            success=True,
            remaining_games=credit.available_games,
            message=f"{credit.available_games} games remaining"
        )
    except Exception as e:
        print(f"Error consuming game credit: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Failed to consume game credit")

@app.post("/api/game-credits/add-from-ad", response_model=ConsumeGameResponse)
def add_game_from_ad(current_user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """
    광고 시청으로 게임 1회 추가
    """
    try:
        credit = get_or_create_game_credit(db, current_user.id)
        credit = check_and_reset_daily(db, credit)

        # 게임 1회 추가
        credit.available_games += 1
        db.commit()
        db.refresh(credit)

        return ConsumeGameResponse(
            success=True,
            remaining_games=credit.available_games,
            message=f"Ad reward added. Total: {credit.available_games} games"
        )
    except Exception as e:
        print(f"Error adding game from ad: {e}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Failed to add game credit")

# ================== 계정 삭제 엔드포인트 ==================
@app.post("/api/delete-account", response_model=DeleteAccountResponse)
def delete_account(
    req: DeleteAccountRequest,
    db: Session = Depends(get_db)
):
    """
    계정 삭제 (영구 삭제, 복구 불가)

    삭제 항목:
    - 사용자 계정 정보 (이메일, 유저네임, 비밀번호)
    - 친구 관계 (Friendship)
    - 친구 요청 (FriendRequest)
    - 채팅 메시지 (ChatMessage)
    - 배틀 요청 (BattleRequest)
    - 게임 크레딧 (GameCredit) - 구독 토큰 포함
    - 프로필 사진 파일

    보존 항목:
    - 게임 기록 (GameHistory, GameMove) - 통계 목적
    """
    try:
        print(f"[DELETE ACCOUNT] Request received for email: {req.email}, username: {req.username}")

        # 1. 사용자 인증 (이메일, 유저네임, 비밀번호 모두 일치해야 함)
        user = db.execute(
            select(User).where(
                User.email == req.email,
                User.username == req.username
            )
        ).scalar_one_or_none()

        if not user:
            print(f"[DELETE ACCOUNT] User not found: {req.email}, {req.username}")
            raise HTTPException(status_code=404, detail="계정을 찾을 수 없습니다. 이메일과 유저네임을 확인해주세요.")

        # 비밀번호 확인
        if not verify_password(req.password, user.password):
            print(f"[DELETE ACCOUNT] Password mismatch for user: {user.username}")
            raise HTTPException(status_code=401, detail="비밀번호가 일치하지 않습니다.")

        user_id = user.id
        username = user.username
        profile_image = user.profile_image

        print(f"[DELETE ACCOUNT] Authentication successful for user_id: {user_id}, username: {username}")

        # 2. 친구 관계 삭제 (양방향)
        friendships_deleted = db.execute(
            select(func.count()).select_from(Friendship).where(
                (Friendship.user_id == user_id) | (Friendship.friend_id == user_id)
            )
        ).scalar()

        db.execute(
            Friendship.__table__.delete().where(
                (Friendship.user_id == user_id) | (Friendship.friend_id == user_id)
            )
        )
        print(f"[DELETE ACCOUNT] Deleted {friendships_deleted} friendships")

        # 3. 친구 요청 삭제 (보낸 것, 받은 것)
        friend_requests_deleted = db.execute(
            select(func.count()).select_from(FriendRequest).where(
                (FriendRequest.sender_id == user_id) | (FriendRequest.receiver_id == user_id)
            )
        ).scalar()

        db.execute(
            FriendRequest.__table__.delete().where(
                (FriendRequest.sender_id == user_id) | (FriendRequest.receiver_id == user_id)
            )
        )
        print(f"[DELETE ACCOUNT] Deleted {friend_requests_deleted} friend requests")

        # 4. 채팅 메시지 삭제 (보낸 것, 받은 것)
        chat_messages_deleted = db.execute(
            select(func.count()).select_from(ChatMessage).where(
                (ChatMessage.sender_id == user_id) | (ChatMessage.receiver_id == user_id)
            )
        ).scalar()

        db.execute(
            ChatMessage.__table__.delete().where(
                (ChatMessage.sender_id == user_id) | (ChatMessage.receiver_id == user_id)
            )
        )
        print(f"[DELETE ACCOUNT] Deleted {chat_messages_deleted} chat messages")

        # 5. 배틀 요청 삭제
        battle_requests_deleted = db.execute(
            select(func.count()).select_from(BattleRequest).where(
                (BattleRequest.sender_id == user_id) | (BattleRequest.receiver_id == user_id)
            )
        ).scalar()

        db.execute(
            BattleRequest.__table__.delete().where(
                (BattleRequest.sender_id == user_id) | (BattleRequest.receiver_id == user_id)
            )
        )
        print(f"[DELETE ACCOUNT] Deleted {battle_requests_deleted} battle requests")

        # 6. 게임 크레딧 삭제 (구독 토큰 포함)
        game_credit_deleted = db.execute(
            select(func.count()).select_from(GameCredit).where(GameCredit.user_id == user_id)
        ).scalar()

        db.execute(
            GameCredit.__table__.delete().where(GameCredit.user_id == user_id)
        )
        print(f"[DELETE ACCOUNT] Deleted game credit (purchase_token cleared)")

        # 7. 프로필 이미지 파일 삭제
        if profile_image:
            profile_image_path = os.path.join(PROFILE_IMAGES_DIR, profile_image)
            if os.path.exists(profile_image_path):
                try:
                    os.remove(profile_image_path)
                    print(f"[DELETE ACCOUNT] Deleted profile image: {profile_image_path}")
                except Exception as e:
                    print(f"[DELETE ACCOUNT] Failed to delete profile image: {e}")

        # 8. 사용자 계정 삭제 (마지막에 삭제)
        db.delete(user)

        # 9. 변경사항 커밋
        db.commit()

        print(f"[DELETE ACCOUNT] Successfully deleted account: {username} (user_id: {user_id})")
        print(f"[DELETE ACCOUNT] Summary: {friendships_deleted} friendships, {friend_requests_deleted} friend requests, {chat_messages_deleted} chat messages")

        return DeleteAccountResponse(
            success=True,
            message=f"계정이 성공적으로 삭제되었습니다. 그동안 Valhalla of Quoridor를 이용해주셔서 감사합니다."
        )

    except HTTPException:
        # HTTPException은 그대로 전달
        raise

    except Exception as e:
        print(f"[DELETE ACCOUNT] Unexpected error: {e}")
        print(f"[DELETE ACCOUNT] Error type: {type(e).__name__}")
        db.rollback()
        raise HTTPException(status_code=500, detail=f"계정 삭제 중 오류가 발생했습니다: {str(e)}")

@app.get("/delete-account")
def serve_delete_account_page():
    """
    계정 삭제 HTML 페이지 서빙

    URL: https://valhallaofquoridor.duckdns.org/delete-account
    """
    return FileResponse("static/delete-account.html")

@app.get("/privacy-policy")
def serve_privacy_policy_page():
    """
    개인정보처리방침 HTML 페이지 서빙

    URL: https://valhallaofquoridor.duckdns.org/privacy-policy
    """
    return FileResponse("static/voq_privacy_policy.html")


