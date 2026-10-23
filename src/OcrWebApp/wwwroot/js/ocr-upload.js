document.addEventListener('DOMContentLoaded', () => {
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const fileListContainer = document.getElementById('fileListContainer');
    const fileListBody = document.getElementById('fileListBody');
    const fileCountSpan = document.getElementById('fileCount');
    const clearFilesBtn = document.getElementById('clearFilesBtn');
    const startOcrBtn = document.getElementById('startOcrBtn');

    const ocrLanguage = document.getElementById('ocrLanguage');
    const pageRangeInput = document.getElementById('pageRangeInput');
    const preprocessGrayscale = document.getElementById('preprocessGrayscale');
    const preprocessContrast = document.getElementById('preprocessContrast');
    const preprocessDeskew = document.getElementById('preprocessDeskew');

    const processingCard = document.getElementById('processingCard');
    const globalProgressBar = document.getElementById('globalProgressBar');
    const batchSummaryBadge = document.getElementById('batchSummaryBadge');
    const batchJobsList = document.getElementById('batchJobsList');
    const batchSpinner = document.getElementById('batchSpinner');

    let selectedFiles = [];
    const allowedExtensions = ['.jpg', '.jpeg', '.png', '.tiff', '.tif', '.pdf'];
    const maxSizeBytes = 25 * 1024 * 1024; // 25 MB

    // Drag & Drop Listeners
    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('dragover');
        });
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('dragover');
        });
    });

    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        if (dt && dt.files && dt.files.length > 0) {
            handleFiles(dt.files);
        }
    });

    fileInput.addEventListener('change', () => {
        if (fileInput.files && fileInput.files.length > 0) {
            handleFiles(fileInput.files);
        }
    });

    function handleFiles(files) {
        let errors = [];

        Array.from(files).forEach(file => {
            const ext = '.' + file.name.split('.').pop().toLowerCase();
            if (!allowedExtensions.includes(ext)) {
                errors.push(`File "${file.name}" không hợp lệ. Chỉ chấp nhận JPG, PNG, TIFF, PDF.`);
                return;
            }

            if (file.size > maxSizeBytes) {
                errors.push(`File "${file.name}" quá lớn (${window.DocuOCR.formatFileSize(file.size)}). Giới hạn là 25 MB.`);
                return;
            }

            // Avoid duplicate by name and size
            if (!selectedFiles.some(f => f.name === file.name && f.size === file.size)) {
                selectedFiles.push(file);
            }
        });

        if (errors.length > 0) {
            alert(errors.join('\n'));
        }

        renderSelectedFiles();
    }

    function renderSelectedFiles() {
        if (selectedFiles.length === 0) {
            fileListContainer.classList.add('d-none');
            startOcrBtn.disabled = true;
            fileListBody.innerHTML = '';
            fileCountSpan.textContent = '0';
            return;
        }

        fileListContainer.classList.remove('d-none');
        startOcrBtn.disabled = false;
        fileCountSpan.textContent = selectedFiles.length;
        fileListBody.innerHTML = '';

        selectedFiles.forEach((file, index) => {
            const ext = file.name.split('.').pop().toUpperCase();
            const row = document.createElement('tr');
            row.innerHTML = `
                <td class="fw-medium text-truncate" style="max-width: 250px;">
                    <i class="bi bi-file-earmark-text text-primary me-1"></i> ${file.name}
                </td>
                <td><span class="badge bg-secondary-subtle text-secondary">${ext}</span></td>
                <td class="small text-muted">${window.DocuOCR.formatFileSize(file.size)}</td>
                <td class="text-end">
                    <button type="button" class="btn btn-outline-danger btn-sm py-0 px-2 remove-file-btn" data-index="${index}">
                        <i class="bi bi-x"></i>
                    </button>
                </td>
            `;
            fileListBody.appendChild(row);
        });

        document.querySelectorAll('.remove-file-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const idx = parseInt(btn.getAttribute('data-index'), 10);
                selectedFiles.splice(idx, 1);
                renderSelectedFiles();
            });
        });
    }

    clearFilesBtn.addEventListener('click', () => {
        selectedFiles = [];
        fileInput.value = '';
        renderSelectedFiles();
    });

    // Start OCR Processing
    startOcrBtn.addEventListener('click', async () => {
        if (selectedFiles.length === 0) return;

        startOcrBtn.disabled = true;
        startOcrBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status"></span> Đang tải file lên...`;

        const formData = new FormData();
        selectedFiles.forEach(file => {
            formData.append('Files', file);
        });
        formData.append('Language', ocrLanguage.value);
        if (pageRangeInput && pageRangeInput.value.trim()) {
            formData.append('PageRange', pageRangeInput.value.trim());
        }
        formData.append('PreprocessGrayscale', preprocessGrayscale.checked);
        formData.append('PreprocessContrast', preprocessContrast.checked);
        formData.append('PreprocessDeskew', preprocessDeskew.checked);

        try {
            const csrfToken = window.DocuOCR.getCsrfToken();
            const response = await fetch('/Job/Upload', {
                method: 'POST',
                headers: {
                    'X-CSRF-TOKEN': csrfToken
                },
                body: formData
            });

            const result = await response.json();

            if (!result.success || !result.jobs || result.jobs.length === 0) {
                alert(result.errors?.join('\n') || 'Không thể tải file lên xử lý.');
                startOcrBtn.disabled = false;
                startOcrBtn.innerHTML = `<i class="bi bi-play-circle-fill me-2"></i> Bắt đầu nhận diện (Start OCR)`;
                return;
            }

            // Clear selected files queue
            selectedFiles = [];
            renderSelectedFiles();

            // Display live processing
            startBatchMonitoring(result.jobs);

        } catch (error) {
            console.error('Lỗi khi tải file:', error);
            alert('Lỗi kết nối máy chủ khi tải file lên.');
            startOcrBtn.disabled = false;
            startOcrBtn.innerHTML = `<i class="bi bi-play-circle-fill me-2"></i> Bắt đầu nhận diện (Start OCR)`;
        }
    });

    function startBatchMonitoring(jobs) {
        processingCard.classList.remove('d-none');
        batchJobsList.innerHTML = '';

        jobs.forEach(job => {
            const item = document.createElement('div');
            item.className = 'list-group-item p-3 d-flex justify-content-between align-items-center';
            item.id = `batchJobItem-${job.id}`;
            item.innerHTML = `
                <div>
                    <div class="fw-semibold text-truncate" style="max-width: 300px;">
                        <i class="bi bi-file-earmark-text text-primary me-1"></i> ${job.fileName}
                    </div>
                    <div class="small text-muted" id="jobStatusText-${job.id}">Đang chờ vào hàng đợi...</div>
                </div>
                <div id="jobAction-${job.id}">
                    <span class="badge bg-secondary-subtle text-secondary">Chờ xử lý</span>
                </div>
            `;
            batchJobsList.appendChild(item);
        });

        const jobIds = jobs.map(j => j.id).join(',');
        const pollInterval = setInterval(async () => {
            try {
                const res = await fetch(`/Job/BatchStatus?ids=${jobIds}`);
                if (!res.ok) return;

                const statusList = await res.json();
                let completedCount = 0;

                statusList.forEach(st => {
                    const statusTextEl = document.getElementById(`jobStatusText-${st.id}`);
                    const actionEl = document.getElementById(`jobAction-${st.id}`);

                    if (st.statusNumber === 2) { // Completed
                        completedCount++;
                        if (statusTextEl) {
                            const conf = st.averageConfidence ? ` (Độ tin cậy: ${st.averageConfidence.toFixed(0)}%)` : '';
                            statusTextEl.innerHTML = `<span class="text-success"><i class="bi bi-check2-circle me-1"></i> Hoàn tất${conf}</span>`;
                        }
                        if (actionEl) {
                            actionEl.innerHTML = `
                                <a href="/Job/Detail/${st.id}" class="btn btn-sm btn-primary">
                                    <i class="bi bi-eye me-1"></i> Xem kết quả
                                </a>
                            `;
                        }
                    } else if (st.statusNumber === 3) { // Failed
                        completedCount++;
                        if (statusTextEl) {
                            statusTextEl.innerHTML = `<span class="text-danger"><i class="bi bi-x-circle me-1"></i> Thất bại: ${st.errorMessage || 'Lỗi không xác định'}</span>`;
                        }
                        if (actionEl) {
                            actionEl.innerHTML = `
                                <a href="/Job/Detail/${st.id}" class="btn btn-sm btn-outline-danger">Chi tiết</a>
                            `;
                        }
                    } else if (st.statusNumber === 1) { // Processing
                        if (statusTextEl) {
                            const pageInfo = st.totalPages > 1 ? ` (Trang ${st.processedPages}/${st.totalPages})` : '';
                            statusTextEl.innerHTML = `<span class="text-warning"><span class="spinner-border spinner-border-sm me-1"></span> Đang nhận diện OCR${pageInfo}...</span>`;
                        }
                        if (actionEl) {
                            actionEl.innerHTML = `<span class="badge bg-warning-subtle text-warning">Đang xử lý</span>`;
                        }
                    }
                });

                const percent = Math.round((completedCount / statusList.length) * 100);
                globalProgressBar.style.width = `${percent}%`;

                if (completedCount >= statusList.length) {
                    clearInterval(pollInterval);
                    batchSpinner.classList.add('d-none');
                    batchSummaryBadge.className = 'badge bg-success';
                    batchSummaryBadge.textContent = 'Hoàn tất toàn bộ!';
                    startOcrBtn.disabled = false;
                    startOcrBtn.innerHTML = `<i class="bi bi-play-circle-fill me-2"></i> Bắt đầu nhận diện (Start OCR)`;
                } else {
                    batchSummaryBadge.textContent = `Đang xử lý (${completedCount}/${statusList.length})`;
                }

            } catch (e) {
                console.error("Lỗi khi cập nhật trạng thái:", e);
            }
        }, 2000);
    }
});
