# 🎓 ClassPluse

> **Smart Academic Attendance & Lecture Management System**  
> Powered by ASP.NET Core 10, Dynamic Cryptographic QR Tokens, and Deep Metric Facial Recognition (ResNet-34).

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?logo=nuget&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Bootstrap 5](https://img.shields.io/badge/UI-Bootstrap%205-7952B3?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 📌 Overview

**ClassPluse** is a secure, enterprise-ready academic attendance platform engineered to eliminate "buddy punching" (proxy attendance) and manual administrative overhead in schools, colleges, and universities.

It enforces a **zero-trust, two-factor physical presence verification pipeline**:
1. **Dynamic Cryptographic QR Tokens:** A lecture hall projection screen broadcasts dynamic QR codes rotating every 15 seconds, encrypted with **AES-256** and signed with **HMAC-SHA256**.
2. **Server-Side Facial Biometrics:** Students scan the QR code using their web browser, which triggers their device camera to capture a live selfie. The server extracts a **128-dimensional Euclidean vector embedding** via deep convolutional neural networks (ResNet-34) and matches it against their registered profile ($d \le 0.60$).
3. **Continuous Learning:** When a student verifies with high confidence ($d < 0.35$), the system silently updates their baseline face vector to adapt to natural physical changes (hair, facial hair, glasses).

---

## 🚀 Key Features

### 👨‍💼 1. System Administration
- **User Provisioning:** Create Student, Faculty, and Admin accounts with automatic credential generation and TLS email dispatch.
- **Academic Course Setup:** Map courses, departments, and assign primary faculty instructors.
- **Course Enrollment:** Manage student enrollments with database-enforced unique constraints to prevent duplicates.
- **Institutional Analytics:** Real-time dashboard with student/faculty distribution and enrollment breakdown charts.

### 👨‍🏫 2. Faculty Management
- **Live Lecture Projection View:** Full-screen presentation mode designed for classroom projectors with auto-refreshing QR tokens and real-time attendee counters.
- **Class Attendance Matrix:** Interactive, spreadsheet-like grid tracking every student against all completed lecture sessions.
- **Excel Export (.xlsx):** Generate formatted, multi-column Excel attendance reports using `ClosedXML`.
- **Manual Attendance Adjustment:** Audit trail-backed manual overrides (`Present`, `Absent`, `Excused`) for medical leaves or exceptional scenarios.

### 👨‍🎓 3. Student Portal
- **Zero-Install Web Experience:** Works seamlessly across iOS Safari, Android Chrome, and desktop browsers via standard HTML5 WebRTC (`getUserMedia`).
- **Biometric Onboarding:** Guided face registration with an on-screen alignment oval.
- **Personal Dashboard & Defaulter Warnings:** Course-by-course attendance percentage tracking with automated alert banners when falling below the statutory 75% threshold.
- **Detailed History Logs:** Timestamped session attendance history with status indicators.

### 🤖 4. Background Automation
- **Hosted Cleanup Service (`AttendanceCleanupService`):** Background worker running hourly to scan sessions ended > 6 hours prior, automatically marking unmarked enrolled students as `Absent` with `MarkedBy = System`.

---

## 🛠️ Technology Stack

| Layer | Technology | Details |
| :--- | :--- | :--- |
| **Framework** | ASP.NET Core 10.0 | MVC + RESTful API Controllers |
| **Language** | C# 13 | High-performance, strongly typed asynchronous backend |
| **Database** | Microsoft SQL Server | Relational persistence with EF Core 10 (Code-First) |
| **Authentication** | ASP.NET Core Identity | Cookie auth, PBKDF2 password hashing, Role-Based Access Control |
| **Biometrics** | `FaceRecognitionDotNet` | Dlib ResNet-34 128-d metric embeddings & 68 landmark predictors |
| **Cryptography** | `System.Security.Cryptography` | AES-256 (CBC) + HMAC-SHA256 tamper-proof tokens |
| **QR Engine** | `QRCoder` (1.8.0) | High-speed server-side PNG Base64 QR generator (`ECCLevel.Q`) |
| **Client Scanner** | `Html5-QRCode` | Fast browser camera scanner without app installation |
| **Excel Export** | `ClosedXML` (0.105.1) | OpenXML native spreadsheet builder |
| **UI & Charts** | Bootstrap 5, Bootstrap Icons, Chart.js | Clean, responsive off-white interface with interactive charts |

---

## 🏗️ Architecture & Verification Flow

```
+----------------------------------------------------------------------------------+
|                              VERIFICATION PIPELINE                                |
+----------------------------------------------------------------------------------+

 1. PROJECTOR SCREEN (Faculty)
    [ Dynamic QR Code ] <--- Rotates every 15s (AES-256 + HMAC-SHA256 Token)
           │
           │ (Scanned by Student Phone)
           ▼
 2. STUDENT DEVICE (Browser)
    [ Step 1: Decode Token ] ---> [ Step 2: Live Front Camera Selfie Capture ]
           │
           │ POST /api/AttendanceApi/mark { token, faceImageBase64 }
           ▼
 3. SERVER VERIFICATION ENGINE
    ├─► [Biometrics]: Dlib ResNet-34 extracts 128-d vector. Matches profile (d <= 0.60)?
    ├─► [Continuous Learning]: If distance d < 0.35, update baseline profile embedding.
    ├─► [Cryptography]: Decrypt AES token -> Verify HMAC-SHA256 signature.
    ├─► [Academic Rules]: Active session? Student enrolled? Duplicate check?
           │
           ▼
 4. PERSISTENCE (SQL Server)
    [ INSERT AttendanceRecord (Status = Present, MarkedBy = Scan, UtcNow, IP) ]
           │
           ▼
 5. REAL-TIME FEEDBACK
    ├─► Student: Success checkmark & redirect to Dashboard
    └─► Projector: Live attendee count increments immediately (+1)
```

---

## 📋 Prerequisites

Before running the application, make sure you have:
1. **.NET 10.0 SDK (x64)** installed ([Download .NET](https://dotnet.microsoft.com/download)).
2. **Microsoft SQL Server** (LocalDB, Express, or Enterprise Edition).
3. **Webcam / Camera** on your test device for facial recognition and QR scanning.

---

## ⚙️ Installation & Setup

### 1. Clone the Repository
```bash
git clone https://github.com/your-username/ClassPluse.git
cd ClassPluse/ClassPluse
```

### 2. Download AI Model Files
ClassPluse uses Dlib's pre-trained deep learning models for facial landmarks and 128-dimensional embeddings. Ensure the following two files are placed inside the `FaceModels/` folder:
- `shape_predictor_68_face_landmarks.dat`
- `dlib_face_recognition_resnet_model_v1.dat`

> **Note:** The `FaceModels/` directory is automatically created at runtime if missing, but the `.dat` model files must be placed inside it for face recognition to operate.

### 3. Configure `appsettings.json`
Update your connection string and settings in `ClassPluse/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ClassPluseDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Secrets": {
    "QrSecretKey": "YourSuperSecretKeyThatIsAtLeast32BytesLong!"
  },
  "SmtpSettings": {
    "Server": "smtp.gmail.com",
    "Port": 587,
    "SenderName": "ClassPluse System",
    "SenderEmail": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

### 4. Apply Database Migrations
Run the Entity Framework Core migrations to create the database schema:
```bash
dotnet ef database update
```

### 5. Run the Application
```bash
dotnet run
```
Navigate to `https://localhost:7198` (or the URL shown in your console).

---

## 🔑 Default Credentials (Development Seeding)

On the initial database migration, the system automatically seeds default roles (`Admin`, `Faculty`, `Student`) and a default Administrator account:

| Attribute | Default Value |
| :--- | :--- |
| **Email** | `admin@classpluse.com` |
| **Password** | `Admin@123` |
| **Role** | `Admin` |

> ⚠️ **Important:** Change this default password immediately after your first login when deploying to production!

---

## 📁 Project Directory Structure

```text
ClassPluse/
├── Controllers/
│   ├── AccountController.cs          # Login, Logout, Password Reset
│   ├── AdminController.cs            # Users, Courses, Enrollments management
│   ├── FacultyController.cs          # Sessions, Live Screen, Matrix, Excel Export
│   ├── StudentController.cs          # QR Scanning, Face Setup, Dashboard
│   ├── AttendanceApiController.cs    # REST Endpoint for attendance verification
│   └── SessionApiController.cs       # REST Endpoint for dynamic QR polling & stats
├── Data/
│   └── AttendanceDbContext.cs        # EF Core DbContext with relational indexes & rules
├── Extensions/
│   └── ServiceCollectionExtensions.cs # DI Container registration helper
├── FaceModels/                       # Dlib .dat neural network weights
├── Models/
│   ├── ApplicationUser.cs            # IdentityUser with 128-d FaceEncoding
│   ├── Course.cs                     # Course entity
│   ├── Enrollment.cs                 # Student-to-Course associative entity
│   ├── LectureSession.cs             # Individual lecture sessions
│   ├── QrToken.cs                    # Dynamic encrypted QR tokens
│   └── AttendanceRecord.cs           # Core attendance ledger
├── Services/
│   ├── AttendanceVerificationService.cs # Multi-stage attendance validation gatekeeper
│   ├── FaceRecognitionService.cs     # Dlib ResNet-34 vector extraction & distance
│   ├── CryptoService.cs              # AES-256 encryption & HMAC-SHA256 signature
│   ├── QrTokenService.cs             # Dynamic QR payload assembly & PNG rendering
│   ├── AttendanceCleanupService.cs   # IHostedService for automated absentee processing
│   └── SmtpEmailService.cs           # TLS SMTP credentials & notification dispatcher
├── Views/                            # Razor Views (Admin, Faculty, Student, Shared)
└── Program.cs                        # Application pipeline, DI, & Database Seeding
```

---

## 🔒 Security & Anti-Fraud Features

- **No Proxy Marking:** Even if a student takes a picture of the projected QR code and sends it via WhatsApp, the absent recipient **cannot mark attendance without passing the live facial biometric check**.
- **Tamper-Proof Tokens:** QR tokens carry an HMAC-SHA256 signature. Any tampering with session IDs or timestamps immediately fails decryption.
- **Short-Lived Rotation:** QR codes refresh on a 15-second cycle. Expired tokens are rejected.
- **Duplicate Prevention:** A composite unique database constraint on `(LectureSessionId, StudentId)` prevents double-submissions.
- **Cross-Platform HTTPS Requirement:** Client-side camera operations require SSL/TLS, protecting video frame streams and credentials in transit.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!  
Feel free to fork this repository, submit pull requests, or open an issue on GitHub.

