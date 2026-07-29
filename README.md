<!-- HEADER BANNER -->
<p align="center">
  <img src="https://capsule-render.vercel.app/api?type=waving&color=gradient&customColorList=1e3c72,2a5298&height=200&section=header&text=E-Learning%20System&fontSize=55&fontColor=ffffff&desc=Centralized%20Platform%20for%20Course%20Registration%20and%20Assessment&descAlignY=75" width="100%" alt="E-Learning System Banner" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/Frontend-WPF-5C2D91?style=for-the-badge&logo=.net&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/Backend-Web%20API-68217A?style=for-the-badge&logo=dotnet&logoColor=white" alt="Web API" />
</p>

---

## 📖 Overview

**SE104_E-learningSystem** is a full-stack digital platform built to replace manual, paper-based, and fragmented classroom processes for course registration, content delivery, and student assessment. Developed as part of the software engineering curriculum (SE104), the system acts as a centralized hub for educational interactions, initially serving internal students and faculty.

As the platform evolves across future releases, it is designed to extend toward integration with external academic databases, global payment gateways for premium certifications, and third-party video conferencing APIs — enabling fully remote, synchronous learning environments.

---

## ✨ Key Features

- **Course Registration:** Digitized enrollment flow replacing manual, paper-based registration processes.
- **Content Delivery:** Centralized distribution of learning materials to students through a structured, role-based interface.
- **Assessment Management:** Streamlined creation, submission, and grading of student assessments.
- **Client-Server Architecture:** Decoupled WPF desktop client communicating with a dedicated Web API backend.
- **Data Transfer Objects (DTOs):** Clean contracts between client and server that keep the API surface consistent and validated.
- **Extensible Design:** Architecture prepared for future integration with external databases, payment gateways, and video conferencing APIs.

---

## 🏛️ System Architecture

This project follows a **Client-Server Architecture**, separating the desktop experience from the backend services that power it.

1. **Presentation Layer (Client):** Built with WPF (`App.xaml`, `Views`), providing a rich, event-driven desktop interface for students and faculty.
2. **Application Layer (Helpers / Models / DTOs):** Shared logic, data models, and data transfer objects that structure how information flows between the client and the API.
3. **Service Layer (Services):** Encapsulates calls to the backend, keeping the UI decoupled from raw HTTP/API details.
4. **Backend (WebAPI_E_learning):** A dedicated ASP.NET Web API project exposing endpoints for registration, content, and assessment data, backed by its own data store.

---

## 🛠️ Technology Stack

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Frontend / GUI** | `C#` / `WPF` | XAML-based desktop client for students and faculty. |
| **Backend API** | `C#` / `ASP.NET Web API` | RESTful services powering registration, content, and assessment. |
| **Data Layer** | `Models` / `DTOs` | Structured domain models and transfer objects shared across layers. |
| **IDE** | `Visual Studio` | Primary development environment. |

---

## 📂 Project Structure

Core folders in this repository:

*   **`Views`**: WPF windows and user controls for the client application.
*   **`Models`**: Domain entities representing core system data.
*   **`DTOs`**: Data Transfer Objects used for client–server communication.
*   **`Services`**: Client-side service classes that call the Web API.
*   **`Helpers`**: Shared utility and helper logic used across the client.
*   **`WebAPI_E_learning`**: The ASP.NET Web API backend project.
*   **`Documents`**: Project documentation and supporting materials.

---

## 🚀 Getting Started

Follow these instructions to set up the project on your local machine for development and testing.

### Prerequisites
*   **Visual Studio 2019/2022** (with .NET desktop development workload installed)
*   **.NET Framework / .NET SDK** matching the project's target (see `.csproj` files)
*   **Microsoft SQL Server** (Express or Developer edition), if the backend requires a database

### Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/miyxotkem/SE104_E-learningSystem.git
   ```

2. **Open the solution:**
   - Open `e-learning app.sln` in Visual Studio.

3. **Configure the backend:**
   - Set `WebAPI_E_learning` as a startup project (or run it separately) so the client has an API to call.
   - Update any connection strings or API base URLs in `App.config` to match your local environment.

4. **Build and Run:**
   - Set the WPF client project as the **Startup Project** (or configure multiple startup projects to run client + API together).
   - Press `F5` or click **Start** in Visual Studio to build and launch the application.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!
If you would like to contribute:

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
