# Ứng Dụng Web Nhận Diện Văn Bản OCR (OCR Document Recognition Web Application)

Hệ thống web nhận diện văn bản OCR hoàn chỉnh, độc lập và chuẩn Production-ready xây dựng trên nền tảng **ASP.NET Core MVC (.NET 9)**, **Tesseract OCR (Tiếng Việt & Tiếng Anh)** và **Entity Framework Core với SQL Server**.

---

## 1. Tính Năng Nổi Bật

- **Đầu vào đa định dạng**: Hỗ trợ ảnh (`.jpg`, `.jpeg`, `.png`, `.tiff`, `.tif`) và tài liệu **PDF đa trang**.
- **Xử lý PDF thông minh**:
  - Tự động kiểm tra và trích xuất trực tiếp lớp văn bản (Text Layer) có sẵn từ PDF bằng `PdfPig`.
  - Đối với PDF dạng scan (ảnh), tự động render từng trang thành ảnh chất lượng cao và chạy OCR, bảo toàn thứ tự trang.
- **Tiền xử lý ảnh (Image Preprocessing)**:
  - Chuyển sang ảnh thang xám (Grayscale).
  - Tăng cường độ tương phản (Contrast Enhancement).
  - Tự động xoay thẳng và chỉnh góc nghiêng (Auto-deskew).
- **Động cơ OCR mã nguồn mở**:
  - Tích hợp Tesseract 5 với bộ dữ liệu huấn luyện `vie.traineddata` (Tiếng Việt có dấu) và `eng.traineddata` (Tiếng Anh).
  - Kiến trúc trừu tượng hóa qua giao diện `IOcrEngine`, sẵn sàng cắm thêm các engine AI/OCR khác trong tương lai mà không ảnh hưởng mã nguồn.
  - Cung cấp điểm tin cậy (Confidence Score) và dữ liệu Bounding Box tọa độ từ.
- **Xử lý bất đồng bộ trong Background (Background Processing)**:
  - Hàng đợi ngầm `IBackgroundOcrQueue` kết hợp `BackgroundOcrWorker`.
  - Giới hạn xử lý đồng thời (`ConcurrencyLimit`) bằng `SemaphoreSlim`, ngăn ngừa cạn kiệt CPU/RAM.
  - **Tự động phục hồi khi restart (Crash Recovery)**: Tự động phát hiện và đưa các tác vụ `Pending` / `Processing` dở dang vào hàng đợi khi ứng dụng khởi động lại.
- **Giao diện làm việc song song (Side-by-side Viewer & Editor)**:
  - Xem trước ảnh tài liệu gốc hoặc trang PDF bên trái kèm điều khiển Zoom phóng to / thu nhỏ.
  - Xem và trực tiếp chỉnh sửa văn bản OCR bên phải.
  - Phân trang tài liệu PDF trực quan (Trang trước / Trang sau).
  - Nút sao chép vào bộ nhớ tạm (Copy to Clipboard) và nút lưu thay đổi vào cơ sở dữ liệu.
- **Xuất kết quả đa định dạng thực tế (Real Export)**:
  - **TXT**: Văn bản thuần UTF-8 (kèm BOM hiển thị chuẩn trên Windows).
  - **DOCX**: Tài liệu Microsoft Word định dạng chuẩn tạo bởi `DocumentFormat.OpenXml` (có ngắt trang rõ ràng).
  - **PDF**: Tài liệu PDF chuẩn tạo bởi `PdfSharpCore`.
- **Cấu hình & Giới hạn an toàn (Limits & Data Retention)**:
  - **Dung lượng file tối đa**: 25 MB (`MaxFileSizeBytes`).
  - **Giới hạn số trang**: 50 trang/file (`MaxPdfPages`) đối với PDF hoặc TIFF đa trang, hỗ trợ tùy chọn lọc dải trang (Page Range, ví dụ: `1-5, 8`).
  - **Thời gian lưu trữ dữ liệu (Retention Policy)**: Tự động dọn dẹp file vật lý và lịch sử sau 24 giờ (`RetentionHours`) qua tiến trình ngầm `DataRetentionWorker`.
- **Lịch sử & Bảo mật theo phiên (Session Privacy)**:
  - Cô lập công việc của từng người dùng qua Cookie `OCR_USER_SESSION`. Người dùng không cần đăng nhập nhưng vẫn được bảo vệ sự riêng tư, không nhìn thấy dữ liệu của người khác.
  - Xác thực Magic bytes (Header binary) chống giả mạo định dạng file.
  - Ngăn chặn triệt để tấn công Path Traversal.
  - Chống CSRF trên toàn bộ các endpoint thay đổi dữ liệu (`[ValidateAntiForgeryToken]`).
  - Lưu trữ file tải lên ngoài `wwwroot` an toàn trong thư mục bảo mật riêng.

---

## 2. Cấu Trúc Dự Án

```
C:\Git\OCR\
├── OcrDocumentRecognition.sln         # File Solution Visual Studio (.sln)
├── OcrDocumentRecognition.slnx        # File Solution hiện đại (.slnx)
├── .gitignore                         # Git ignore chuẩn toàn diện cho Visual Studio
├── README.md                          # Tài liệu hướng dẫn chi tiết
└── src/
    └── OcrWebApp/
        ├── Controllers/
        │   ├── HomeController.cs      # Tiếp nhận tải lên, drag & drop, hiển thị gần đây
        │   └── JobController.cs       # Quản lý vòng đời OCR, chi tiết, lưu, xuất file, xóa
        ├── Data/
        │   ├── AppDbContext.cs        # EF Core DbContext, cấu hình Fluent API và Index
        │   └── Migrations/            # EF Core Migrations cho SQL Server
        ├── Models/
        │   ├── Entities/              # OcrJob, OcrJobPage, OcrJobStatus
        │   ├── DTOs/                  # UploadRequestDto, JobStatusResponseDto, SaveEditedTextRequest
        │   └── ViewModels/            # HomeViewModel, JobDetailViewModel, JobHistoryViewModel
        ├── Options/
        │   └── OcrSettings.cs         # Cấu hình lưu trữ, giới hạn kích thước, timeout, worker
        ├── Services/
        │   ├── Abstractions/          # IOcrEngine, IFileSecurityService, IDocumentProcessingService, IExportService, IBackgroundOcrQueue
        │   └── Implementations/       # TesseractOcrEngine, FileSecurityService, DocumentProcessingService, ExportService, BackgroundOcrWorker
        ├── Middlewares/
        │   └── UserSessionMiddleware.cs # Quản lý session cookie bảo mật người dùng
        ├── Views/
        │   ├── Home/Index.cshtml      # Giao diện kéo thả file, tùy chọn cấu hình & tiến trình live
        │   ├── Job/Detail.cshtml      # Giao diện xem song song ảnh gốc & sửa văn bản
        │   ├── Job/History.cshtml     # Bảng quản lý lịch sử, thống kê & thao tác xuất/xóa
        │   └── Shared/                # _Layout.cshtml, Error.cshtml, _ValidationScriptsPartial
        ├── wwwroot/
        │   ├── css/site.css           # Giao diện hiện đại, responsive
        │   └── js/                    # ocr-upload.js, ocr-viewer.js, site.js
        └── tessdata/                  # Chứa file mô hình ngôn ngữ eng.traineddata & vie.traineddata
```

---

## 3. Yêu Cầu Môi Trường & Hướng Dẫn Cài Đặt

### Yêu cầu:
- **.NET 9 SDK** (hoặc .NET 10 SDK tương thích ngược).
- **SQL Server** hoặc **SQL Server LocalDB** (mặc định đi kèm Visual Studio).

### Cấu hình chuỗi kết nối:
File `appsettings.json` đã cấu hình sẵn chuỗi kết nối đến `MSSQLLocalDB`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=OcrDocumentDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```
Khi triển khai lên server production, bạn có thể ghi đè biến môi trường:
```bash
ConnectionStrings__DefaultConnection="Server=YOUR_SERVER;Database=OcrDocumentDb;User Id=...;Password=...;TrustServerCertificate=True"
```
hoặc sử dụng .NET User Secrets trong môi trường phát triển:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
```

### Chạy ứng dụng:
1. Mở file solution `OcrDocumentRecognition.sln` bằng **Visual Studio 2022 / 2025** và bấm **F5** (hoặc `Ctrl + F5`).
2. Hoặc chạy qua dòng lệnh Terminal:
   ```bash
   cd C:\Git\OCR\src\OcrWebApp
   dotnet run
   ```
3. Truy cập trình duyệt tại địa chỉ được thông báo trên màn hình console (ví dụ: `http://localhost:5113` hoặc `https://localhost:7113`).
Ứng dụng sẽ tự động chạy migration khởi tạo bảng trong SQL Server và tải các mô hình cần thiết.
