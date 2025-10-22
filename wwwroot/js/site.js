$(document).ready(function () {

    const spinner = $('#loadingSpinner');

    // ===================================================
    // === 1. HELPER FUNCTIONS (Spinner)
    // ===================================================

    // Hiển thị spinner
    function showSpinner() {
        spinner.removeClass('hidden');
    }

    // Ẩn spinner
    function hideSpinner() {
        spinner.addClass('hidden');
    }

    // ===================================================
    // === 2. EVENT HANDLERS (Send Reset Link) - MỚI
    // ===================================================

    // Xử lý click nút "Send Reset Link"
    // Chúng ta dùng 'body' .on 'click' để đảm bảo nó hoạt động ngay cả khi bảng được tải lại qua AJAX (nếu có)
    $('body').on('click', '.send-reset-link-btn', function () {
        var $this = $(this);
        var username = $this.data('username');
        var email = $this.data('email');
        var token = $('#RequestVerificationToken').val(); // Lấy AntiForgeryToken từ Index.cshtml

        if (!email) {
            alert('Error: This user does not have an email address specified in AD.');
            return;
        }

        if (!confirm('Are you sure you want to send a password reset link to ' + username + ' (' + email + ')?')) {
            return;
        }

        // Vô hiệu hóa nút và hiển thị spinner
        $this.prop('disabled', true).text('Sending...');
        showSpinner();

        $.ajax({
            url: '/Management/SendResetLink', // Action chúng ta đã tạo
            type: 'POST',
            data: {
                __RequestVerificationToken: token, // Gửi kèm token
                username: username,
                userEmail: email
            },
            success: function (response) {
                if (response.success) {
                    alert(response.message); // Thông báo thành công
                } else {
                    alert('Error: ' + response.message); // Thông báo lỗi
                }
            },
            error: function (xhr, status, error) {
                alert('An unexpected error occurred: ' + error);
            },
            complete: function () {
                // Khôi phục nút và ẩn spinner
                $this.prop('disabled', false).text('Send Reset Link');
                hideSpinner();
            }
        });
    });

});