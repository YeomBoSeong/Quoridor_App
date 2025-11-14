# Valhalla of Quoridor

A multiplayer Quoridor game application with real-time matchmaking, friend system, and AI opponents.

## Tech Stack

**Back-end**: FastAPI, SQLite, SQLAlchemy, Uvicorn, WebSocket, JWT, BCrypt

**Front-end**: Unity 2021.3, C#, Unity UI, TextMeshPro

**Infrastructure**: AWS EC2, Nginx, SSL/TLS (Let's Encrypt)

**External APIs**: Gmail API (Email verification), Unity IAP, Google Mobile Ads

## Project Structure

```
for_github/
├── Server/                 # FastAPI Backend
│   ├── main.py            # Main FastAPI application
│   ├── User.py            # User models and authentication
│   ├── email_sender.py    # Email verification system
│   ├── quoridor_ai.py     # AI opponent logic
│   ├── static/            # Static files
│   └── .env.example       # Environment variables template
│
└── Client(Unity)/         # Unity Client Scripts
    └── *.cs               # C# scripts for Unity game client
```

## Server Setup

### Prerequisites

- Python 3.8+
- pip

### Installation

1. Navigate to the Server directory:
```bash
cd Server
```

2. Create a virtual environment:
```bash
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
```

3. Install dependencies:
```bash
pip install fastapi uvicorn sqlalchemy python-jose passlib python-dotenv bcrypt email-validator aiosqlite python-multipart
```

4. Set up environment variables:
```bash
cp .env.example .env
```

Edit `.env` file and configure:
- `SECRET_KEY`: Generate using `python -c "import secrets; print(secrets.token_hex(32))"`
- `SENDER_EMAIL`: Your Gmail address
- `SENDER_PASSWORD`: Your Gmail app password ([How to get](https://support.google.com/accounts/answer/185833))

5. Run the server:
```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

For production with HTTPS:
- Configure Nginx as reverse proxy
- Obtain SSL certificate with Let's Encrypt (Certbot)

## Client Setup

### Unity Configuration

1. Open your Unity project (Unity 2021.3+)

2. Copy the C# scripts from `Client(Unity)/` to your Unity project's `Assets/Scripts/` folder

3. Update `ServerConfig.cs`:
   - Replace `your-server-domain.com` with your actual server domain/IP
   - Configure the port (default: 443 for HTTPS)

### Required Unity Packages

- TextMeshPro
- Unity UI (uGUI)
- Newtonsoft JSON
- Unity IAP (optional, for in-app purchases)
- Google Mobile Ads (optional, for ads)

## Features

- **User Authentication**: JWT-based authentication with email verification
- **Friend System**: Send/accept friend requests, view friends list
- **Real-time Chat**: WebSocket-based chat with friends
- **Matchmaking**: Rapid and Blitz game modes with ELO rating
- **AI Opponent**: Play against AI with multiple difficulty levels
- **Game History**: Track and replay past games
- **Profile Management**: Customize profile with image upload

## Security Notes

- Never commit the `.env` file to version control
- Keep your `SECRET_KEY` and email credentials secure
- Use HTTPS in production
- Regularly update dependencies for security patches

## License

This project is for portfolio purposes.
