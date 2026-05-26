# BÁO CÁO TỔNG HỢP DỰ ÁN MÔN HỌC
## Môn: Phân tích Thiết kế Hệ thống (SE104)
## Đề tài: **SmartEdu — Hệ thống E-Learning Quản lý Khóa học và Kiểm tra Trực tuyến**

---

## 1. Đặt vấn đề & Tóm tắt bài toán (Introduction & Problem Statement)

### 1.1. Lý do chọn đề tài

Trong bối cảnh chuyển đổi số giáo dục ngày càng được đẩy mạnh, các trường đại học vẫn còn phụ thuộc nhiều vào quy trình thủ công, phân mảnh trong việc quản lý khóa học, giao bài tập và tổ chức kiểm tra. Sinh viên đăng ký học phần qua các kênh rời rạc (bảng tin, email, nhóm mạng xã hội); giảng viên phân phát tài liệu học tập và đề thi theo cách truyền thống; kết quả kiểm tra được lưu trữ phân tán, gây khó khăn trong tra cứu và thống kê.

Nhóm lựa chọn đề tài **SmartEdu** nhằm thiết kế một nền tảng e-learning tích hợp, thay thế toàn bộ quy trình trên bằng một hệ thống số hóa thống nhất. Theo tài liệu *Vision & Scope* của nhóm, SmartEdu định vị là *"một trung tâm tương tác giáo dục tập trung, phục vụ sinh viên và giảng viên nội bộ trong giai đoạn đầu, với lộ trình tích hợp cơ sở dữ liệu học thuật bên ngoài, cổng thanh toán chứng chỉ và API hội nghị truyền hình trong các phiên bản tương lai."*

### 1.2. Mục tiêu hệ thống

Hệ thống SmartEdu hướng đến ba mục tiêu cốt lõi:

1. **Số hóa vòng đời khóa học**: Cho phép giảng viên tạo, quản lý và phân phối nội dung (bài giảng, bài tập) theo từng lớp học và học kỳ.
2. **Tự động hóa quy trình kiểm tra**: Cung cấp môi trường tạo đề, tổ chức thi và chấm điểm trực tuyến với các tùy chọn nâng cao (ngẫu nhiên hóa câu hỏi, giới hạn thời gian, đa lần thử).
3. **Minh bạch hóa thông tin học tập**: Cung cấp dashboard thống kê theo thời gian thực cho cả sinh viên (lịch sử bài thi, điểm số) lẫn giảng viên (báo cáo lớp, tỷ lệ đạt).

### 1.3. Phạm vi giải quyết

Phiên bản hiện tại (Release 1) tập trung vào phân hệ **quản lý khóa học**, **kiểm tra trực tuyến** và **quản lý người dùng**, được triển khai dưới dạng ứng dụng desktop (WPF / C#) kết nối với backend Firebase (Firestore + Firebase Auth).

### 1.4. Đối tượng sử dụng chính

Hệ thống phục vụ ba nhóm actor chính:

| Actor | Vai trò chính trong hệ thống |
|---|---|
| **Sinh viên (Student)** | Đăng ký khóa học, xem bài giảng, làm bài kiểm tra, tra cứu lịch sử điểm số |
| **Giảng viên (Teacher)** | Tạo và quản lý khóa học, soạn đề thi, xem báo cáo kết quả lớp, nhận xét bài làm |
| **Quản trị viên (Admin)** | Quản lý tài khoản người dùng, cấp/thu hồi quyền truy cập, theo dõi hoạt động hệ thống |

---

## 2. Phương pháp Khơi gợi Yêu cầu (Requirement Elicitation Methods)

### 2.1. Quy trình thu thập yêu cầu

Nhóm áp dụng phương pháp elicitation đa tầng, kết hợp ba kỹ thuật bổ trợ nhau:

#### (a) Phỏng vấn có cấu trúc (Structured Interview)
Nhóm tổ chức các buổi phỏng vấn trực tiếp với giảng viên bộ môn đóng vai trò **domain expert / khách hàng**. Mỗi buổi phỏng vấn được chuẩn bị trước danh sách câu hỏi tập trung vào ba chủ đề: (i) quy trình hiện tại mà giảng viên đang áp dụng trong quản lý lớp học, (ii) những điểm đau (pain points) trong việc tổ chức kiểm tra, và (iii) kỳ vọng về hệ thống mới. Kết quả đầu ra là tập **yêu cầu thô (raw requirements)** bao gồm các tính năng ưu tiên cao như: tạo đề thi trắc nghiệm, phân quyền theo vai trò, và xuất báo cáo điểm.

#### (b) Phân tích nghiệp vụ & Tài liệu miền (Domain Analysis)
Nhóm nghiên cứu quy trình đăng ký học phần, tổ chức thi và lưu trữ kết quả hiện hành của nhà trường. Các tài liệu nghiệp vụ thu thập được dùng để xây dựng **business rules**, sau đó được hình thức hóa trong tài liệu `SmartEdu_BusinessRules.docx`. Ví dụ điển hình: quy tắc *"Sinh viên chỉ có thể làm lại bài thi tối đa N lần (MaxAttempts), trong đó N do giảng viên cấu hình"* — một ràng buộc nghiệp vụ trực tiếp chi phối thiết kế thuộc tính `AllowMultipleAttempts` và `MaxAttempts` trong class `Exam`.

#### (c) Phân rã & Ưu tiên hóa yêu cầu (Feature Backlog Refinement)
Các yêu cầu thô sau khi thu thập được phân loại thành ba mức ưu tiên (Must-have / Should-have / Nice-to-have) theo kỹ thuật **MoSCoW prioritization** và ghi nhận vào `SmartEdu_Features_Backlog.csv`. Tập hợp yêu cầu được hình thức hóa toàn diện trong **Software Requirements Specification (SRS)** (`SmartEdu_SRS.docx`), bao gồm cả yêu cầu chức năng (functional requirements) và phi chức năng (non-functional requirements: hiệu năng, bảo mật, khả năng mở rộng).

### 2.2. Kết quả thu được

Sau quá trình elicitation, nhóm thu được bộ tài liệu yêu cầu gồm:
- **Vision & Scope Document**: Phạm vi sản phẩm và định hướng phát triển dài hạn.
- **SRS (Software Requirements Specification)**: Đặc tả chi tiết 20+ use case, bao gồm luồng chính, luồng thay thế và điều kiện ngoại lệ.
- **Business Rules Document**: 15+ ràng buộc nghiệp vụ ràng buộc trực tiếp đến thiết kế dữ liệu.
- **Feature Backlog**: Danh sách tính năng có thứ tự ưu tiên cho Release 1 và các phiên bản kế tiếp.

---

## 3. Các mô hình Thiết kế Hệ thống (System Modeling)

### 3.1. Use Case Diagram — Phạm vi và Chức năng Hệ thống

**Use Case Diagram** đóng vai trò bản đồ chức năng của hệ thống, xác định rõ ba actor (Student, Teacher, Admin) và tập hợp các use case mà từng actor tương tác. Các use case trọng tâm bao gồm:

- **Student**: *Đăng ký khóa học (Course Registration)*, *Làm bài kiểm tra (Take Quiz)*, *Xem lịch sử bài thi (View Quiz History)*, *Xem kết quả chi tiết (View Quiz Result Detail)*.
- **Teacher**: *Tạo khóa học (Create Course)*, *Soạn đề thi (Create Exam)*, *Quản lý câu hỏi (Manage Questions)*, *Xem báo cáo lớp (View Exam Report)*, *Nhận xét bài làm (Give Feedback)*.
- **Admin**: *Quản lý tài khoản (Manage Users)*, *Khóa/Mở khóa người dùng (Block/Unblock User)*, *Xem thống kê hệ thống (View Dashboard)*.

Use Case Diagram là điểm xuất phát để nhóm xác định **ranh giới hệ thống (system boundary)** và làm cơ sở triển khai các mô hình tiếp theo.

### 3.2. Class Diagram — Cấu trúc Dữ liệu và Quan hệ Đối tượng

**Class Diagram** mô hình hóa cấu trúc tĩnh của hệ thống thông qua 13 lớp nghiệp vụ chính được triển khai trong thư mục `Models/`:

| Class | Vai trò nghiệp vụ |
|---|---|
| `User` | Đại diện cho mọi tài khoản trong hệ thống; phân biệt vai trò qua thuộc tính `Role` |
| `Course` | Khóa học/lớp học; liên kết với `InstructorId` và `CourseRegistration` |
| `Exam` | Bài thi với cấu hình nâng cao (thời gian, số lần thử, điểm đạt) |
| `ExamQuestion` | Câu hỏi thi; được tổng hợp bởi `Exam` qua danh sách `QuestionIds` |
| `ExamSubmission` | Bài làm của sinh viên; ghi nhận câu trả lời (`AnswerResponse`) và trạng thái (`SubmissionStatus`) |
| `CourseContent` / `Lesson` | Nội dung bài giảng trong khóa học |
| `Assignment` / `Submission` | Bài tập về nhà và lượt nộp bài |
| `Notification` | Thông báo hệ thống đến người dùng |
| `Comment` | Phản hồi/thảo luận trong khóa học |

Các quan hệ chính: `Course` **1—\*** `Exam`; `Exam` **1—\*** `ExamQuestion`; `Exam` **1—\*** `ExamSubmission`; `ExamSubmission` **1—\*** `AnswerResponse`. Đây là nền tảng để thiết kế schema Firestore collections tương ứng.

### 3.3. Sequence Diagram — Luồng Tương tác Theo Thời gian

**Sequence Diagram** hiện thực hóa từng use case cụ thể bằng cách mô tả trình tự trao đổi thông điệp giữa các đối tượng. Hai luồng được đặc tả chi tiết nhất:

**Luồng "Sinh viên làm bài thi" (Take Quiz):**
> `StudentQuizView` → `FirebaseService.GetExam()` → `Firestore` → trả về `Exam + QuestionIds` → `TakeQuizView` tải câu hỏi → sinh viên trả lời → `ExamSubmission` được khởi tạo → `FirebaseService.SubmitExam()` → ghi vào Firestore → `QuizResultDetailView` hiển thị kết quả.

**Luồng "Giảng viên tạo đề thi" (Create Exam):**
> `CreateExamView` → validate form → `ExamDraft` được lưu tạm → `CreateExamQuestionsView` soạn câu hỏi → `FirebaseService.SaveExam()` → publish lên Firestore.

Sequence Diagram giúp nhóm phát hiện ra các **điểm đồng bộ hóa dữ liệu** tiềm ẩn (ví dụ: trạng thái bài thi `IsPublished` phải được cập nhật đồng thời với `QuestionIds`) mà Class Diagram không thể hiện được.

### 3.4. Statechart Diagram — Vòng đời Trạng thái Đối tượng

**Statechart Diagram** được xây dựng cho hai thực thể có vòng đời trạng thái phức tạp nhất:

**Vòng đời `ExamSubmission`:**
```
[Khởi tạo] → InProgress → Submitted → Graded
                                   ↘ Expired (hết thời gian)
```
Diagram này trực tiếp ánh xạ đến enum `SubmissionStatus {InProgress, Submitted, Graded, Expired}` trong code và xác định các **guard conditions**: chuyển từ `Submitted` sang `Graded` chỉ xảy ra khi giảng viên nhấn "Chấm điểm" hoặc hệ thống tự động tính điểm với câu hỏi trắc nghiệm.

**Vòng đời `Exam`:**
```
[Draft] → (Published, IsActive=true) → Ongoing → Closed
                                     ↘ Cancelled (giảng viên thu hồi)
```
Statechart này là cơ sở để giảng viên hiểu tại sao một bài thi đã published không thể chỉnh sửa câu hỏi mà không thu hồi về Draft trước.

### 3.5. Mối liên kết giữa các mô hình

Các mô hình không tồn tại độc lập mà có quan hệ truy xuất lẫn nhau theo luồng từ trừu tượng đến cụ thể:

```
Use Case Diagram  (Định nghĩa "Hệ thống làm gì")
       ↓
Class Diagram     (Định nghĩa "Dữ liệu được cấu trúc thế nào")
       ↓
Sequence Diagram  (Định nghĩa "Các đối tượng tương tác ra sao để thực hiện use case")
       ↓
Statechart Diagram (Định nghĩa "Đối tượng thay đổi trạng thái như thế nào qua vòng đời")
```

Ví dụ cụ thể: Use case *"Take Quiz"* → được đặc tả (SRS) → hiện thực hóa qua Sequence Diagram → sử dụng các class `Exam`, `ExamSubmission`, `AnswerResponse` từ Class Diagram → trạng thái của `ExamSubmission` được quản lý bởi Statechart.

---

## 4. Thảo luận & Bài học Kinh nghiệm (Discussions & Lessons Learned)

### 4.1. Thiết kế dữ liệu ban đầu bị thiếu thực thể / quan hệ

**Khó khăn**: Trong phiên bản Class Diagram đầu tiên, nhóm chỉ mô hình hóa `Exam` và `User` mà bỏ qua thực thể trung gian `ExamSubmission`. Hệ quả là không có nơi lưu trữ câu trả lời từng câu hỏi của sinh viên (`AnswerResponse`), làm cho chức năng "Xem kết quả chi tiết" không thể thiết kế được ở bước Sequence Diagram.

**Giải quyết**: Nhóm quay lại phân tích use case *"View Quiz Result Detail"* từ góc độ dữ liệu cần hiển thị, từ đó truy ngược ra các thực thể còn thiếu. Đây là minh chứng rõ ràng cho nguyên tắc **iterative modeling**: các mô hình phải được làm mịn qua nhiều vòng (refinement cycles), không thể hoàn thiện ngay từ đầu.

**Bài học**: Khi thiết kế Class Diagram, cần đặt câu hỏi *"Dữ liệu này được hiển thị ở đâu và ai cần đọc nó?"* cho mỗi thực thể, thay vì chỉ mô hình hóa theo trực giác.

### 4.2. Xung đột ý kiến khi vẽ Sequence Diagram

**Khó khăn**: Khi xây dựng Sequence Diagram cho luồng nộp bài, nhóm phát sinh tranh luận: *"Điểm số nên được tính phía client (ứng dụng WPF) hay phía server (Cloud Function)?"* Hai thành viên bảo vệ hai luồng thiết kế khác nhau, dẫn đến hai phiên bản Sequence Diagram song song tồn tại trong một thời gian.

**Giải quyết**: Nhóm tổ chức review session, so sánh hai phương án theo tiêu chí: (i) tính nhất quán dữ liệu (data integrity), (ii) khả năng gian lận (cheating prevention), (iii) độ phức tạp triển khai. Phương án tính điểm phía server được chọn vì đảm bảo tốt hơn hai tiêu chí đầu. Sequence Diagram được thống nhất và cập nhật lại.

**Bài học**: Xung đột trong quá trình modeling là bình thường và **có giá trị** — nó buộc nhóm phải tư duy rõ ràng hơn về trade-off thiết kế. Cần có tiêu chí đánh giá rõ ràng để phân giải xung đột, tránh quyết định dựa trên cảm tính.

### 4.3. Yêu cầu thay đổi sau khi đã thiết kế (Late Requirement Change)

**Khó khăn**: Sau khi Class Diagram và Sequence Diagram đã hoàn thiện, khách hàng (giảng viên bộ môn) yêu cầu bổ sung tính năng *"Giảng viên có thể để lại nhận xét (FeedbackFromTeacher) trên từng bài làm"*. Điều này yêu cầu cập nhật `ExamSubmission` (thêm thuộc tính `FeedbackFromTeacher`, `GradedAt`) và điều chỉnh Sequence Diagram luồng *"Chấm điểm"*.

**Giải quyết**: Nhóm xử lý thay đổi theo nguyên tắc **impact analysis**: trước tiên liệt kê tất cả các mô hình và module bị ảnh hưởng bởi thay đổi, sau đó cập nhật tuần tự từ Class Diagram → SRS → Sequence Diagram → code. Nhờ đó tránh được tình trạng mô hình và code bị lệch nhau (model-code divergence).

**Bài học**: Yêu cầu thay đổi muộn là không thể tránh khỏi trong thực tế. Hệ thống tài liệu nhất quán (SRS, models) giúp đánh giá **phạm vi ảnh hưởng (impact scope)** nhanh chóng và chính xác, từ đó giảm chi phí thay đổi.

### 4.4. Khoảng cách giữa mô hình và hiện thực triển khai

**Khó khăn**: Statechart Diagram mô tả trạng thái `Exam` có state `Cancelled`, nhưng khi triển khai, nhóm nhận ra Firebase Firestore không hỗ trợ atomic state transitions — việc cập nhật trạng thái `IsActive = false` và xóa `QuestionIds` phải được xử lý thủ công trong transaction.

**Giải quyết**: Nhóm bổ sung **implementation notes** vào Sequence Diagram để ghi nhận ràng buộc kỹ thuật này, giúp tách biệt rõ logic nghiệp vụ (business logic) khỏi ràng buộc công nghệ (technology constraint).

**Bài học**: Mô hình UML thể hiện **what** (hệ thống làm gì), không phải **how** (làm thế nào ở mức kỹ thuật). Khi triển khai, cần thêm lớp implementation notes để lấp đầy khoảng cách này.

---

## 5. Kết luận (Conclusion)

Dự án SmartEdu đã hoàn thành mục tiêu Release 1 với mức độ phủ chức năng đáp ứng tốt yêu cầu ban đầu. Cụ thể:

| Mục tiêu ban đầu | Mức độ hoàn thành | Ghi chú |
|---|---|---|
| Quản lý khóa học & lịch giảng | ✅ Hoàn thành | Teacher: tạo/sửa/xóa khóa học; Student: xem & đăng ký |
| Hệ thống kiểm tra trực tuyến | ✅ Hoàn thành | Đầy đủ luồng tạo đề → làm bài → chấm điểm → xem kết quả |
| Phân quyền 3 vai trò | ✅ Hoàn thành | Admin / Teacher / Student với giao diện riêng biệt |
| Báo cáo & thống kê | ✅ Hoàn thành | ExamReport (Teacher), Dashboard (Admin & Student) |
| Tích hợp video conferencing | 🔄 Để lại Release 2 | Nằm trong roadmap Vision & Scope |

Về phương diện phân tích thiết kế, nhóm nhận thấy bốn mô hình UML (Use Case, Class, Sequence, Statechart) khi được xây dựng có hệ thống và theo đúng thứ tự phụ thuộc đã hỗ trợ hiệu quả cho quá trình triển khai: giảm thiểu sai sót về logic nghiệp vụ, phát hiện sớm các yêu cầu mâu thuẫn và cung cấp ngôn ngữ chung (common language) để nhóm thống nhất ý kiến.

Kinh nghiệm quan trọng nhất rút ra là: **phân tích thiết kế không phải là giai đoạn tuyến tính một chiều, mà là một quá trình lặp liên tục (iterative process)** — trong đó mỗi mô hình mới xây dựng đều có thể phản hồi ngược lại và làm mịn các mô hình trước đó. Đây chính là bản chất của Requirement Refinement trong thực tế kỹ nghệ phần mềm.

---

> *Báo cáo được soạn thảo dựa trên codebase thực tế của dự án SmartEdu (d:\SE104\e-learning app), bộ tài liệu SRS/Vision & Scope/Business Rules, và quá trình phân tích hệ thống của nhóm.*
