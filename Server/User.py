# User.py
from datetime import datetime
from typing import Optional, List
from fastapi import HTTPException, Depends
from fastapi.security import OAuth2PasswordBearer
from jose import jwt, JWTError
from passlib.context import CryptContext
from pydantic import BaseModel, Field, EmailStr
from sqlalchemy import Column, Integer, String, DateTime, select, Text, ForeignKey, Boolean, Float
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import Session
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# ================== 설정 ==================
SECRET_KEY = os.getenv("SECRET_KEY", "your-secret-key-here-change-this-in-production")  # .env 파일에서 로드
ALGORITHM = "HS256"
ACCESS_TOKEN_EXPIRE_MINUTES = 60 * 24 * 7  # 7일

# ================== DB 모델 ==================
Base = declarative_base()

class User(Base):
    __tablename__ = "users"
    id           = Column(Integer, primary_key=True, index=True)
    username     = Column(String(64), unique=True, index=True, nullable=False)
    password     = Column(String(255), nullable=False)  # bcrypt 해시
    email        = Column(String(255), unique=True, index=True, nullable=False)
    elo_rating   = Column(Integer, default=1500, nullable=False)  # 기존 ELO 레이팅 (호환성)
    rapid_elo    = Column(Integer, default=1500, nullable=False)  # Rapid ELO 레이팅
    blitz_elo    = Column(Integer, default=1500, nullable=False)  # Blitz ELO 레이팅
    profile_image = Column(String(500), nullable=True, default=None)  # 프로필 사진 파일명
    created_at   = Column(DateTime, default=datetime.utcnow)
    session_token = Column(String(255), nullable=True, default=None)  # 현재 활성 세션 토큰
    status       = Column(String(20), default="offline", nullable=False)  # online, offline, in_game, matchmaking
    last_active  = Column(DateTime, default=datetime.utcnow)  # 마지막 활동 시간

class Friendship(Base):
    __tablename__ = "friendships"
    id         = Column(Integer, primary_key=True, index=True)
    user_id    = Column(Integer, nullable=False)  # 친구 요청을 보낸 사용자 ID
    friend_id  = Column(Integer, nullable=False)  # 친구가 된 사용자 ID
    created_at = Column(DateTime, default=datetime.utcnow)

class FriendRequest(Base):
    __tablename__ = "friend_requests"
    id         = Column(Integer, primary_key=True, index=True)
    sender_id  = Column(Integer, nullable=False)    # 요청을 보낸 사용자 ID
    receiver_id = Column(Integer, nullable=False)   # 요청을 받은 사용자 ID
    status     = Column(String(20), default="pending", nullable=False)  # pending, accepted, rejected
    created_at = Column(DateTime, default=datetime.utcnow)

class ChatMessage(Base):
    __tablename__ = "chat_messages"
    id         = Column(Integer, primary_key=True, index=True)
    sender_id  = Column(Integer, ForeignKey("users.id"), nullable=False)     # 메시지를 보낸 사용자 ID
    receiver_id = Column(Integer, ForeignKey("users.id"), nullable=False)   # 메시지를 받은 사용자 ID
    message    = Column(Text, nullable=False)                               # 메시지 내용
    is_read    = Column(Boolean, default=False, nullable=False)             # 읽음 여부
    created_at = Column(DateTime, default=datetime.utcnow)                  # 생성 시간

class GameHistory(Base):
    __tablename__ = "game_histories"
    id                 = Column(Integer, primary_key=True, index=True)
    game_token         = Column(String(50), nullable=False, index=True)           # 게임 토큰
    player1_id         = Column(Integer, ForeignKey("users.id"), nullable=False)  # Player1
    player2_id         = Column(Integer, ForeignKey("users.id"), nullable=False)  # Player2
    winner_id          = Column(Integer, ForeignKey("users.id"), nullable=True)   # 승자
    game_mode          = Column(String(20), nullable=False)                       # "rapid" or "blitz"
    game_start_time    = Column(DateTime, default=datetime.utcnow)                # 게임 시작 시간
    game_end_time      = Column(DateTime, nullable=True)                          # 게임 종료 시간
    game_result        = Column(String(100), nullable=True)                       # 게임 결과 설명
    player1_elo_before = Column(Integer, nullable=True)                           # 게임 전 Player1 ELO
    player1_elo_after  = Column(Integer, nullable=True)                           # 게임 후 Player1 ELO
    player1_elo_change = Column(Integer, nullable=True)                           # Player1 ELO 변화량
    player2_elo_before = Column(Integer, nullable=True)                           # 게임 전 Player2 ELO
    player2_elo_after  = Column(Integer, nullable=True)                           # 게임 후 Player2 ELO
    player2_elo_change = Column(Integer, nullable=True)                           # Player2 ELO 변화량

class GameMove(Base):
    __tablename__ = "game_moves"
    id             = Column(Integer, primary_key=True, index=True)
    game_id        = Column(Integer, ForeignKey("game_histories.id"), nullable=False)  # 게임 참조
    move_number    = Column(Integer, nullable=False)                                   # 수 번호 (1, 2, 3...)
    player_id      = Column(Integer, ForeignKey("users.id"), nullable=False)          # 수를 둔 플레이어
    move_type      = Column(String(20), nullable=False)                               # "move", "wall", "forfeit", "disconnect"
    position_from  = Column(String(20), nullable=True)                               # 시작 위치 또는 이동 위치 (예: "4,0")
    position_to    = Column(String(20), nullable=True)                               # 벽의 끝 위치 (벽인 경우, 예: "4,1")
    remaining_time = Column(Float, nullable=True)                                     # 남은 시간
    move_timestamp = Column(DateTime, default=datetime.utcnow)                        # 수를 둔 시간

class BattleRequest(Base):
    __tablename__ = "battle_requests"
    id         = Column(Integer, primary_key=True, index=True)
    sender_id  = Column(Integer, ForeignKey("users.id"), nullable=False)     # 요청을 보낸 사용자 ID
    receiver_id = Column(Integer, ForeignKey("users.id"), nullable=False)   # 요청을 받은 사용자 ID
    status     = Column(String(20), default="pending", nullable=False)      # pending, accepted, rejected, expired
    created_at = Column(DateTime, default=datetime.utcnow)                  # 생성 시간

class GameCredit(Base):
    __tablename__ = "game_credits"
    id              = Column(Integer, primary_key=True, index=True)
    user_id         = Column(Integer, ForeignKey("users.id"), nullable=False, unique=True, index=True)  # 사용자 ID
    available_games = Column(Integer, default=5, nullable=False)              # 사용 가능한 게임 횟수
    last_reset_date = Column(DateTime, default=datetime.utcnow)               # 마지막 리셋 날짜
    updated_at      = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)  # 업데이트 시간

# ================== Pydantic 모델 ==================
class SignupRequest(BaseModel):
    username: str = Field(min_length=1, max_length=32)
    password: str = Field(min_length=1, max_length=72)
    email: EmailStr

class Token(BaseModel):
    access_token: str
    token_type: str = "bearer"

class MeResponse(BaseModel):
    id: int
    username: str
    email: EmailStr
    created_at: datetime

class ProfileResponse(BaseModel):
    username: str
    rapid_elo: int
    blitz_elo: int
    rapid_percentile: float
    blitz_percentile: float
    profile_image_url: Optional[str] = None

class AddFriendRequest(BaseModel):
    username: str

class FriendRequestResponse(BaseModel):
    message: str
    request_id: int = None

class FriendRequestData(BaseModel):
    id: int
    sender_username: str
    sender_rapid_elo: int
    sender_blitz_elo: int
    created_at: datetime

class FriendData(BaseModel):
    username: str
    rapid_elo: int
    blitz_elo: int
    is_online: bool
    status: str

class FriendsListResponse(BaseModel):
    friends: list[FriendData]

class ChatMessageRequest(BaseModel):
    receiver_username: str
    message: str

class ChatMessageData(BaseModel):
    id: int
    sender_username: str
    receiver_username: str
    message: str
    created_at: datetime
    is_sent_by_me: bool

class ChatMessageResponse(BaseModel):
    success: bool
    message: str

class ChatHistoryResponse(BaseModel):
    messages: List[ChatMessageData]

class UnreadMessageData(BaseModel):
    friend_username: str
    unread_count: int

class UnreadMessagesResponse(BaseModel):
    unread_messages: List[UnreadMessageData]

class GameMoveData(BaseModel):
    move_number: int
    player_username: str
    move_type: str
    position_from: Optional[str] = None
    position_to: Optional[str] = None
    remaining_time: Optional[float] = None
    move_timestamp: str

class GameHistoryData(BaseModel):
    id: int
    game_token: str
    opponent_username: str
    game_mode: str
    result: str
    my_elo_before: int
    my_elo_after: int
    my_elo_change: int
    game_start_time: str
    game_end_time: Optional[str] = None
    game_result: Optional[str] = None

class GameHistoryResponse(BaseModel):
    game_history: List[GameHistoryData]

class GameDetailResponse(BaseModel):
    game_id: int
    game_token: str
    player1_username: str
    player2_username: str
    game_mode: str
    game_start_time: str
    game_end_time: Optional[str] = None
    game_result: Optional[str] = None
    moves: List[GameMoveData]

class UserStatusRequest(BaseModel):
    username: str
    status: str

class UserStatusResponse(BaseModel):
    username: str
    status: str

class BattleRequestData(BaseModel):
    target_username: str

class BattleRequestResponse(BaseModel):
    success: bool
    message: str
    request_id: Optional[int] = None

class VerificationRequest(BaseModel):
    username: str = Field(min_length=1, max_length=32)
    password: str = Field(min_length=1, max_length=72)
    email: EmailStr

class VerificationResponse(BaseModel):
    success: bool
    message: str

class VerifyCodeRequest(BaseModel):
    email: EmailStr
    code: str = Field(min_length=6, max_length=6)

class VerifyCodeResponse(BaseModel):
    success: bool
    message: str
    user_id: Optional[int] = None
    username: Optional[str] = None

class ForgotPasswordRequest(BaseModel):
    email: EmailStr

class ForgotPasswordResponse(BaseModel):
    success: bool
    message: str

class ChangePasswordRequest(BaseModel):
    username: str = Field(min_length=1, max_length=32)
    old_password: str = Field(min_length=1, max_length=72)
    new_password: str = Field(min_length=1, max_length=72)

class ChangePasswordResponse(BaseModel):
    success: bool
    message: str


# ================== 보안 ==================
pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")
oauth2_scheme = OAuth2PasswordBearer(tokenUrl="login")

def verify_password(plain_pw: str, hashed_pw: str) -> bool:
    return pwd_context.verify(plain_pw, hashed_pw)

def hash_password(pw: str) -> str:
    return pwd_context.hash(pw)

def create_access_token(data: dict, expires_delta: Optional[datetime] = None) -> str:
    import secrets
    from datetime import timedelta
    to_encode = data.copy()
    expire = datetime.utcnow() + (expires_delta or timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES))
    
    # 세션 토큰 생성 (중복 로그인 방지용)
    session_token = secrets.token_urlsafe(32)
    to_encode.update({"exp": expire, "session": session_token})
    
    return jwt.encode(to_encode, SECRET_KEY, algorithm=ALGORITHM)

def get_current_user_factory(get_db_func):
    """팩토리 함수로 get_current_user 생성"""
    def get_current_user(token: str = Depends(oauth2_scheme), db: Session = Depends(get_db_func)) -> User:
        cred_exception = HTTPException(status_code=401, detail="Could not validate credentials")
        try:
            payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
            username: str = payload.get("sub")
            session_token: str = payload.get("session")
            if not username or not session_token:
                raise cred_exception
        except JWTError:
            raise cred_exception
        
        user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
        if not user:
            raise cred_exception
            
        # 세션 토큰 검증 (중복 로그인 방지)
        print(f"Validating session for user {username}")
        print(f"DB session token: {user.session_token[:10] if user.session_token else 'None'}...")
        print(f"JWT session token: {session_token[:10]}...")
        
        if user.session_token != session_token:
            print(f"Session mismatch! Rejecting request.")
            raise HTTPException(status_code=401, detail="Session expired due to login from another device")
            
        return user
    return get_current_user

# ================== 사용자 관리 함수 ==================
def create_user(db: Session, signup_data: SignupRequest) -> User:
    """새 사용자 생성"""
    # 중복 가입 방지: username, email 각각 검사
    u_exists = db.execute(select(User).where(User.username == signup_data.username)).scalar_one_or_none()
    if u_exists:
        raise HTTPException(status_code=409, detail="Username already exists")

    e_exists = db.execute(select(User).where(User.email == signup_data.email)).scalar_one_or_none()
    if e_exists:
        raise HTTPException(status_code=409, detail="Email already exists")

    # 비밀번호 해시 후 저장
    hashed = hash_password(signup_data.password)
    
    user = User(
        username=signup_data.username, 
        password=hashed, 
        email=signup_data.email,
        elo_rating=1500,
        created_at=datetime.utcnow()
    )
    
    db.add(user)
    db.commit()
    db.refresh(user)
    
    return user

def authenticate_user(db: Session, username: str, password: str) -> Optional[User]:
    """사용자 인증"""
    user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
    if not user or not verify_password(password, user.password):
        return None
    return user

def update_user_session(db: Session, user: User, session_token: str) -> None:
    """사용자의 세션 토큰 업데이트 (중복 로그인 방지)"""
    user.session_token = session_token
    db.commit()
    db.refresh(user)

def get_user_by_id(db: Session, user_id: int) -> Optional[User]:
    """ID로 사용자 조회"""
    return db.execute(select(User).where(User.id == user_id)).scalar_one_or_none()

def get_user_by_username(db: Session, username: str) -> Optional[User]:
    """사용자명으로 사용자 조회"""
    return db.execute(select(User).where(User.username == username)).scalar_one_or_none()

def calculate_percentile(db: Session, user_elo: int, elo_column: str) -> float:
    """특정 ELO의 백분위 계산"""
    from sqlalchemy import text, func
    
    # 해당 ELO 컬럼의 총 사용자 수
    total_users = db.execute(text(f"SELECT COUNT(*) FROM users")).scalar()
    
    if total_users == 0:
        return 50.0
    
    # 해당 사용자보다 낮은 ELO를 가진 사용자 수
    lower_users = db.execute(text(f"SELECT COUNT(*) FROM users WHERE {elo_column} < :user_elo"), 
                           {"user_elo": user_elo}).scalar()
    
    # 백분위 계산 (0-100%)
    percentile = (lower_users / total_users) * 100
    return round(percentile, 1)

def calculate_elo_change(winner_elo: int, loser_elo: int, k_factor: int = 32) -> tuple:
    """
    ELO 레이팅 변화량 계산
    
    Args:
        winner_elo: 승자의 현재 ELO
        loser_elo: 패자의 현재 ELO
        k_factor: K-factor (레이팅 변화 폭, 기본값 32)
    
    Returns:
        (winner_new_elo, loser_new_elo): 새로운 ELO 값들
    """
    import math
    
    # 예상 승률 계산
    expected_winner = 1 / (1 + math.pow(10, (loser_elo - winner_elo) / 400))
    expected_loser = 1 / (1 + math.pow(10, (winner_elo - loser_elo) / 400))
    
    # 실제 결과 (승자 = 1, 패자 = 0)
    actual_winner = 1
    actual_loser = 0
    
    # ELO 변화량 계산 (승자 기준으로만 계산하여 제로섬 보장)
    winner_change = k_factor * (actual_winner - expected_winner)

    # 변화량을 정수로 반올림 (제로섬 보장을 위해 한 번만 반올림)
    elo_change = round(winner_change)

    # 새로운 ELO 값 계산 (제로섬: 승자 +X, 패자 -X)
    winner_new_elo = winner_elo + elo_change
    loser_new_elo = loser_elo - elo_change

    # 최소 ELO를 100으로 제한
    # 제한 적용 시 제로섬이 깨질 수 있지만, 최소값 보호가 우선
    winner_new_elo = max(100, winner_new_elo)
    loser_new_elo = max(100, loser_new_elo)

    return winner_new_elo, loser_new_elo

def update_user_elo(db: Session, username: str, new_rapid_elo: int = None, new_blitz_elo: int = None) -> bool:
    """
    사용자의 ELO 레이팅 업데이트
    
    Args:
        db: 데이터베이스 세션
        username: 사용자명
        new_rapid_elo: 새로운 Rapid ELO (None이면 업데이트하지 않음)
        new_blitz_elo: 새로운 Blitz ELO (None이면 업데이트하지 않음)
    
    Returns:
        bool: 업데이트 성공 여부
    """
    try:
        user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
        if not user:
            print(f"User {username} not found for ELO update")
            return False
        
        old_rapid = user.rapid_elo
        old_blitz = user.blitz_elo
        
        if new_rapid_elo is not None:
            user.rapid_elo = new_rapid_elo
            # 기존 elo_rating도 업데이트 (호환성)
            user.elo_rating = new_rapid_elo
        
        if new_blitz_elo is not None:
            user.blitz_elo = new_blitz_elo
        
        db.commit()
        db.refresh(user)
        
        print(f"ELO updated for {username}: Rapid {old_rapid} -> {user.rapid_elo}, Blitz {old_blitz} -> {user.blitz_elo}")
        return True
        
    except Exception as e:
        print(f"Error updating ELO for {username}: {e}")
        db.rollback()
        return False

def get_user_elos(db: Session, username: str) -> tuple:
    """
    사용자의 현재 ELO 값들 조회

    Args:
        db: 데이터베이스 세션
        username: 사용자명

    Returns:
        (rapid_elo, blitz_elo): ELO 값들 또는 (1500, 1500) if not found
    """
    try:
        user = db.execute(select(User).where(User.username == username)).scalar_one_or_none()
        if user:
            return user.rapid_elo, user.blitz_elo
        else:
            return 1500, 1500  # 기본값
    except Exception as e:
        print(f"Error getting ELO for {username}: {e}")
        return 1500, 1500

# ================== Game Credit Pydantic 모델 ==================
class GameCreditResponse(BaseModel):
    """게임 크레딧 조회 응답"""
    available_games: int
    can_play: bool
    last_reset_date: datetime

class ConsumeGameResponse(BaseModel):
    """게임 소비 응답"""
    success: bool
    remaining_games: int
    message: str

class DeleteAccountRequest(BaseModel):
    """계정 삭제 요청"""
    email: EmailStr
    username: str
    password: str

class DeleteAccountResponse(BaseModel):
    """계정 삭제 응답"""
    success: bool
    message: str