using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace e_learning_app
{
    public class EduSmartCodeReceiver : LocalServerCodeReceiver
    {
        // Giao diện HTML cực đẹp cho EduSmart
        private const string SuccessHtml = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>EduSmart - Xác thực thành công</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap' rel='stylesheet'>
    <style>
        body {
            margin: 0;
            padding: 0;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            font-family: 'Inter', sans-serif;
            color: #2d3748;
        }
        .card {
            background: rgba(255, 255, 255, 0.95);
            padding: 3rem;
            border-radius: 2rem;
            box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
            text-align: center;
            max-width: 450px;
            width: 90%;
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.2);
            animation: slideUp 0.6s ease-out;
        }
        @keyframes slideUp {
            from { transform: translateY(30px); opacity: 0; }
            to { transform: translateY(0); opacity: 1; }
        }
        .icon-box {
            width: 80px;
            height: 80px;
            background: #f0fff4;
            border-radius: 50%;
            display: flex;
            justify-content: center;
            align-items: center;
            margin: 0 auto 2rem;
        }
        .icon {
            color: #48bb78;
            font-size: 40px;
            font-weight: bold;
        }
        h1 {
            margin: 0 0 1rem;
            color: #1a202c;
            font-size: 1.75rem;
            font-weight: 700;
        }
        p {
            line-height: 1.6;
            color: #4a5568;
            margin-bottom: 1.5rem;
        }
        .status {
            display: inline-block;
            padding: 0.5rem 1.5rem;
            background: #ebf8ff;
            color: #3182ce;
            border-radius: 9999px;
            font-weight: 600;
            font-size: 0.875rem;
            margin-bottom: 2rem;
        }
        .footer {
            font-size: 0.875rem;
            color: #a0aec0;
            margin-top: 2rem;
        }
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon-box'>
            <span class='icon'>✓</span>
        </div>
        <div class='status'>Xác thực thành công</div>
        <h1>EduSmart xin chào!</h1>
        <p>Bạn đã đăng nhập thành công vào hệ thống. Thông tin của bạn đã được bảo mật và đồng bộ.</p>
        <p>Bây giờ bạn có thể đóng cửa sổ trình duyệt này và quay lại ứng dụng để bắt đầu học tập.</p>
        <div class='footer'>
            Hệ thống quản lý học tập EduSmart &copy; 2026
        </div>
    </div>
</body>
</html>";

        // Chỉ cần constructor để truyền mã HTML vào lớp cha là đủ
        public EduSmartCodeReceiver() : base(SuccessHtml)
        {
        }
    }
}
