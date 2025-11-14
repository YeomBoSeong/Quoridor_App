import smtplib
import random
import string
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from typing import Optional
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# ================== 이메일 설정 ==================
# TODO: 실제 이메일 서버 정보로 변경해야 합니다
SMTP_SERVER = "smtp.gmail.com"  # Gmail SMTP 서버
SMTP_PORT = 587  # TLS 포트
SENDER_EMAIL = os.getenv("SENDER_EMAIL", "your-email@gmail.com")  # 발신자 이메일
SENDER_PASSWORD = os.getenv("SENDER_PASSWORD", "your-app-password")  # Gmail 앱 비밀번호

def generate_verification_code() -> str:
    """6자리 랜덤 숫자 인증 코드 생성"""
    return str(random.randint(100000, 999999))

def send_verification_email(recipient_email: str, verification_code: str) -> bool:
    """
    인증 코드를 이메일로 전송

    Args:
        recipient_email: 수신자 이메일 주소
        verification_code: 6자리 인증 코드

    Returns:
        bool: 전송 성공 여부
    """
    try:
        # 이메일 메시지 생성
        message = MIMEMultipart("alternative")
        message["Subject"] = "Your Valhalla of Quoridor Account Verification Code"
        message["From"] = SENDER_EMAIL
        message["To"] = recipient_email

        # 이메일 본문
        text = f"Your verification code is {verification_code}."
        html = f"""
        <html>
          <body>
            <h2>Valhalla of Quoridor Account Verification</h2>
            <p>Your verification code is <strong>{verification_code}</strong>.</p>
            <p>Please enter this code to complete your account creation.</p>
            <p>This code will expire in 10 minutes.</p>
          </body>
        </html>
        """

        # 텍스트와 HTML 버전 추가
        part1 = MIMEText(text, "plain")
        part2 = MIMEText(html, "html")
        message.attach(part1)
        message.attach(part2)

        # SMTP 서버 연결 및 이메일 전송
        with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
            server.starttls()  # TLS 보안 시작
            server.login(SENDER_EMAIL, SENDER_PASSWORD)
            server.sendmail(SENDER_EMAIL, recipient_email, message.as_string())

        print(f"Verification email sent to {recipient_email}")
        return True

    except Exception as e:
        print(f"Failed to send verification email to {recipient_email}: {e}")
        return False

def generate_temporary_password(length: int = 12) -> str:
    """임시 비밀번호 생성 (영문 대소문자 + 숫자)"""
    characters = string.ascii_letters + string.digits
    return ''.join(random.choice(characters) for _ in range(length))

def send_account_recovery_email(recipient_email: str, username: str, temporary_password: str) -> bool:
    """
    계정 정보(유저네임 + 임시 비밀번호)를 이메일로 전송

    Args:
        recipient_email: 수신자 이메일 주소
        username: 사용자 이름
        temporary_password: 임시 비밀번호

    Returns:
        bool: 전송 성공 여부
    """
    try:
        # 이메일 메시지 생성
        message = MIMEMultipart("alternative")
        message["Subject"] = "Your Valhalla of Quoridor Account Information"
        message["From"] = SENDER_EMAIL
        message["To"] = recipient_email

        # 이메일 본문
        text = f"""
Your Valhalla of Quoridor Account Information:

Username: {username}
Temporary Password: {temporary_password}

Please use this temporary password to log in, and change it to a new password as soon as possible.
"""
        html = f"""
        <html>
          <body>
            <h2>Valhalla of Quoridor Account Recovery</h2>
            <p>Here is your account information:</p>
            <table style="border-collapse: collapse;">
              <tr>
                <td style="padding: 8px; font-weight: bold;">Username:</td>
                <td style="padding: 8px;">{username}</td>
              </tr>
              <tr>
                <td style="padding: 8px; font-weight: bold;">Temporary Password:</td>
                <td style="padding: 8px; background-color: #f0f0f0; font-family: monospace;">{temporary_password}</td>
              </tr>
            </table>
            <p style="color: #666; margin-top: 20px;">
              <strong>Important:</strong> Please use this temporary password to log in,
              and change it to a new password as soon as possible for security.
            </p>
          </body>
        </html>
        """

        # 텍스트와 HTML 버전 추가
        part1 = MIMEText(text, "plain")
        part2 = MIMEText(html, "html")
        message.attach(part1)
        message.attach(part2)

        # SMTP 서버 연결 및 이메일 전송
        with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
            server.starttls()  # TLS 보안 시작
            server.login(SENDER_EMAIL, SENDER_PASSWORD)
            server.sendmail(SENDER_EMAIL, recipient_email, message.as_string())

        print(f"Account recovery email sent to {recipient_email}")
        return True

    except Exception as e:
        print(f"Failed to send account recovery email to {recipient_email}: {e}")
        return False
