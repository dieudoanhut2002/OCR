document.addEventListener('DOMContentLoaded', () => {
    const previewImage = document.getElementById('previewImage');
    const zoomInBtn = document.getElementById('zoomInBtn');
    const zoomOutBtn = document.getElementById('zoomOutBtn');
    const zoomResetBtn = document.getElementById('zoomResetBtn');

    const ocrTextArea = document.getElementById('ocrTextArea');
    const charCountEl = document.getElementById('charCount');
    const copyTextBtn = document.getElementById('copyTextBtn');
    const saveTextBtn = document.getElementById('saveTextBtn');
    const saveStatus = document.getElementById('saveStatus');
    const retryJobBtn = document.getElementById('retryJobBtn');

    // 1. Image Zoom Controls
    let currentZoom = 1.0;

    function applyZoom(zoom) {
        currentZoom = Math.min(Math.max(zoom, 0.4), 3.5);
        if (previewImage) {
            previewImage.style.transform = `scale(${currentZoom})`;
        }
    }

    if (zoomInBtn) {
        zoomInBtn.addEventListener('click', () => applyZoom(currentZoom + 0.2));
    }
    if (zoomOutBtn) {
        zoomOutBtn.addEventListener('click', () => applyZoom(currentZoom - 0.2));
    }
    if (zoomResetBtn) {
        zoomResetBtn.addEventListener('click', () => applyZoom(1.0));
    }

    // 2. Character & Word Counting
    function updateCounts() {
        if (!ocrTextArea || !charCountEl) return;
        const text = ocrTextArea.value || '';
        const chars = text.length;
        const words = text.trim() ? text.trim().split(/\s+/).length : 0;
        charCountEl.textContent = `${chars.toLocaleString()} ký tự • ${words.toLocaleString()} từ`;
    }

    if (ocrTextArea) {
        ocrTextArea.addEventListener('input', updateCounts);
        updateCounts();
    }

    // 3. Copy to Clipboard
    if (copyTextBtn && ocrTextArea) {
        copyTextBtn.addEventListener('click', async () => {
            const text = ocrTextArea.value;
            if (!text) return;

            try {
                await navigator.clipboard.writeText(text);
                const originalHtml = copyTextBtn.innerHTML;
                copyTextBtn.innerHTML = `<i class="bi bi-check2 text-success me-1"></i> Đã sao chép!`;
                setTimeout(() => {
                    copyTextBtn.innerHTML = originalHtml;
                }, 2000);
            } catch (err) {
                // Fallback for older browsers
                ocrTextArea.select();
                document.execCommand('copy');
                alert('Đã sao chép văn bản vào bộ nhớ tạm.');
            }
        });
    }

    // 4. Save Edited Text
    if (saveTextBtn && ocrTextArea) {
        saveTextBtn.addEventListener('click', async () => {
            const jobId = saveTextBtn.getAttribute('data-job-id');
            const pageStr = saveTextBtn.getAttribute('data-page');
            const pageNumber = pageStr ? parseInt(pageStr, 10) : null;
            const content = ocrTextArea.value;

            saveTextBtn.disabled = true;
            saveTextBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span> Đang lưu...`;

            try {
                const csrfToken = window.DocuOCR ? window.DocuOCR.getCsrfToken() : '';
                const response = await fetch('/Job/Save', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-CSRF-TOKEN': csrfToken
                    },
                    body: JSON.stringify({
                        jobId: jobId,
                        pageNumber: pageNumber,
                        content: content
                    })
                });

                const data = await response.json();
                if (data.success) {
                    if (saveStatus) {
                        saveStatus.classList.remove('d-none');
                        setTimeout(() => {
                            saveStatus.classList.add('d-none');
                        }, 3000);
                    }
                } else {
                    alert(data.message || 'Lưu thất bại.');
                }
            } catch (err) {
                console.error('Lỗi khi lưu:', err);
                alert('Lỗi kết nối máy chủ khi lưu.');
            } finally {
                saveTextBtn.disabled = false;
                saveTextBtn.innerHTML = `<i class="bi bi-floppy me-1"></i> Lưu thay đổi`;
            }
        });
    }

    // 5. Retry Job
    if (retryJobBtn) {
        retryJobBtn.addEventListener('click', async () => {
            const jobId = retryJobBtn.getAttribute('data-job-id');
            try {
                const csrfToken = window.DocuOCR ? window.DocuOCR.getCsrfToken() : '';
                const res = await fetch(`/Job/Retry/${jobId}`, {
                    method: 'POST',
                    headers: {
                        'X-CSRF-TOKEN': csrfToken
                    }
                });
                const data = await res.json();
                if (data.success) {
                    window.location.reload();
                } else {
                    alert(data.message || 'Lỗi khi đưa vào hàng đợi.');
                }
            } catch (e) {
                alert('Lỗi kết nối máy chủ khi thử lại.');
            }
        });
    }
});
