<!-- HEADER BANNER -->
<p align="center">
  <img src="https://capsule-render.vercel.app/api?type=waving&color=0:1e3c72,100:2a5298&height=200&section=header&text=📚%20SmartEdu%20(SE104)&fontSize=50&fontColor=ffffff&desc=E-Learning%20Platform%20for%20Course%20and%20Online%20Exam%20Management&descAlignY=75" width="100%" alt="Banner" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 9" />
  <img src="https://img.shields.io/badge/Frontend-WPF-5C2D91?style=for-the-badge&logo=.net&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/Backend-ASP.NET%20Web%20API-68217A?style=for-the-badge&logo=dotnet&logoColor=white" alt="Web API" />
  <img src="https://img.shields.io/badge/Database-Firebase%20Firestore-FFCA28?style=for-the-badge&logo=firebase&logoColor=black" alt="Firestore" />
  <img src="https://img.shields.io/badge/Auth-JWT-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white" alt="JWT" />
</p>

---

## 📖 Overview

**SE104_E-learningSystem** (project name: **SmartEdu**) is a full-stack e-learning platform built for the SE104 (System Analysis & Design) course. It digitizes course registration, learning content delivery, assignment submission/grading, and online exams for three roles — **Student**, **Teacher**, and **Admin** — replacing manual, paper-based, and fragmented classroom workflows.

Release 1 is implemented as a **WPF desktop client** talking to a dedicated **ASP.NET Core Web API** backend, with **Google Firestore** as the primary datastore and **Firebase Authentication** concepts backed by a custom **JWT** issuance flow. The repository also includes the team's complete academic documentation set (Vision & Scope, Use Cases, Business Rules, SRS, SDD, Test Suite).

---

## ✨ Key Features

### 👤 Accounts (all roles)
- Email/password sign-in, registration, and password reset flow (`LoginControl`, `RegisterControl`, `ForgotPasswordControl`, `NewPassword`).
- Profile management and in-app notifications (`ProfileManage`, `NotificationsView`).

### 🎓 Student
- Browse and register for courses (`StudentCourseView`), view a personal dashboard (`StudentDashboardView`).
- Take timed online exams/quizzes (`TakeQuizView`, `StudentQuizView`) and review quiz history and detailed results (`QuizHistoryView`, `QuizResultDetailView`).

### 🧑‍🏫 Teacher
- Create and manage courses and class rosters (`CreateCoursesView`, `MyClassesView`, `CourseDetailView`, `TeachingScheduleView`).
- Build exams and questions, edit existing exams, and review exam reports (`CreateExamView`, `CreateExamQuestionsView`, `EditExamView`, `ExamManagementView`, `ExamReportView`, `ExamCardView`).
- Manage assignments: create, grade, and publish grades for student submissions (server-side: `CreateAssignment`, `SubmitAssignment`, `GradeSubmission`, `PublishGrades`).

### 🛠️ Admin
- Central dashboard, user management, and system settings (`AdminDashboardView`, `AdminUsersView`, `AdminSettingsView`).

### ⚙️ Backend API (`WebAPI_E_learning`)
- REST controllers for **Auth**, **Users**, **Courses**, **Exams**, **Comments**, and **Notifications**.
- **JWT Bearer authentication** issued by the API and validated on every protected endpoint.
- **Firebase Admin SDK + Google Cloud Firestore** as the persistence layer, with a custom `FirestoreTimestampConverter` for clean JSON serialization.
- Dockerized (`Dockerfile`) for containerized deployment.

---

## 🏛️ System Architecture

The project follows a **Client-Server Architecture**:

1. **Presentation Layer (WPF Client):** Role-based views under `Views/Student`, `Views/Teacher`, `Views/Admin`, and `Views/Common`, hosted inside role-specific windows (`StudentMainWindow`, `AdminMainWindow`, `MainWindow`, `LoginWindow`).
2. **Application Layer (`Models`, `DTOs`, `Helpers`):** Shared domain models (`Course`, `Exam`, `ExamSubmission`, `Assignment`, `Comment`, `Notification`, `User`, etc.), request/response DTOs, and helpers such as `SecureTokenManager` and `FirestoreMock` (for local/offline testing).
3. **Service Layer (`Services`):** Client-side services encapsulating API calls, auth, and Firebase access — `ApiService`, `AuthService`, `FirebaseService`, `NotificationService`, `DatabaseManager`.
4. **Backend (`WebAPI_E_learning`):** ASP.NET Core Web API exposing `AuthController`, `UsersController`, `CoursesController`, `ExamsController`, `CommentsController`, and `NotificationsController`, backed by Firestore.

---

## 🛠️ Technology Stack

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Client** | `C#` / `WPF` (.NET 9, `net9.0-windows`) | Role-based desktop UI for Student/Teacher/Admin. |
| **Backend API** | `ASP.NET Core` (.NET 9) | REST API secured with JWT Bearer auth. |
| **Database** | `Google Cloud Firestore` | NoSQL document store via Firebase Admin SDK. |
| **Auth** | `JWT` + `Firebase Auth` concepts | Token-based auth issued by the API. |
| **File/Media** | `CloudinaryDotNet` | Media upload/management from the client. |
| **Export** | `ClosedXML` | Excel export functionality. |
| **Containerization** | `Docker` | `Dockerfile` for the Web API. |
| **IDE** | `Visual Studio` | Primary development environment. |

---

## 📂 Project Structure

*   **`Views/Student` / `Views/Teacher` / `Views/Admin` / `Views/Common` / `Views/Windows`**: Role-based WPF views and windows.
*   **`Models`**: Shared domain entities (Course, Exam, Assignment, Comment, Notification, User, etc.).
*   **`DTOs/Requests`, `DTOs/Responses`**: Client-server data contracts.
*   **`Services`**: Client-side services (`ApiService`, `AuthService`, `FirebaseService`, `NotificationService`, `DatabaseManager`, `SecureTokenManager`).
*   **`Helpers`**: Utility classes including `FirestoreMock` for offline/local testing.
*   **`WebAPI_E_learning`**: The ASP.NET Core backend — `Controllers/`, `Models/`, `Helpers/`, Firebase credentials folder, `Dockerfile`.
*   **`Documents/final`**: Full SE104 deliverables — Vision & Scope, Use Cases, Business Rules, SRS, SDD, Requirements, Features Backlog, Test Suite.
*   **`Documents/template`**: Reference/template documents used to produce the final deliverables.
*   **`Documents/test_case_suit`**: Test case suite and a Python test-case generator script.

---

## 🚀 Getting Started

### Prerequisites
*   **Visual Studio 2022** (with .NET desktop development workload) or the **.NET 9 SDK**
*   A **Firebase project** with Firestore enabled and a service account key
*   **Docker** (optional, for running the API in a container)

### Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/miyxotkem/SE104_SmartEdu.git
   ```

2. **Configure Firebase credentials:**
   - Place your Firebase service account JSON at `WebAPI_E_learning/firebase/firebase_json.json`.
   - Update the Firestore project ID in `WebAPI_E_learning/Program.cs` if different from the original.

3. **Configure JWT settings:**
   - Update the `Jwt` section in `WebAPI_E_learning/appsettings.json` (`Key`, `Issuer`, `Audience`) for your environment.

4. **Run the backend:**
   ```bash
   cd WebAPI_E_learning
   dotnet run
   ```
   Or build the Docker image using the provided `Dockerfile`.

5. **Run the WPF client:**
   - Open `e-learning app.sln` in Visual Studio.
   - Set the WPF project as the Startup Project and point `ApiService` at your running API's base URL.
   - Press `F5` to build and launch.

---

## 📄 Documentation

The `Documents/` folder contains the team's full SE104 academic deliverables, including Vision & Scope, Use Case specifications, Business Rules, SRS, SDD, a Requirements spreadsheet, a Features Backlog, and a full Test Suite — alongside a short project report (`short_report_SE104.md`, `Documents/EduSmart_ShortReport.pdf`).

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!

1. Fork the repository.
2. Create a new branch (`git checkout -b feature/YourFeatureName`).
3. Commit your changes (`git commit -m 'Add some feature'`).
4. Push to the branch (`git push origin feature/YourFeatureName`).
5. Open a Pull Request.

---

## 👨‍💻 Team & Collaborators

**Đinh Quang Nhật**  
*Software Engineering Student @ UIT*
* **GitHub:** [@PeterBrr](https://github.com/PeterBrr)
* **Focus:** Full-Stack .NET, System Architecture & API Design

**innguyen**  
*Software Engineering Student @ UIT*
* **GitHub:** [@innguyen](https://github.com/innguyen)
* **Focus:** Full-Stack .NET, System Architecture & API Design

**Thinh Phat Ho**  
*Software Engineering Student @ UIT*
* **GitHub:** [@miyxotkem](https://github.com/miyxotkem)
* **Focus:** Full-Stack .NET, System Architecture & API Design
