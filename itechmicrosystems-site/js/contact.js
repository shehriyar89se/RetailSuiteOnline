// Contact form. The Razor version posted back to the server and re-rendered the
// page; on a static site the form posts JSON to the /api/contact function and
// updates in place. Validation is plain HTML5 constraints — the old page relied
// on jQuery unobtrusive validation, which no longer ships.
(function () {
    var form = document.getElementById('contactForm');
    if (!form) return;

    var successBox = document.getElementById('contactSuccess');
    var errorBox = document.getElementById('contactError');
    var submitBtn = document.getElementById('contactSubmit');

    form.addEventListener('submit', async function (e) {
        e.preventDefault();

        // Bootstrap shows the .invalid-feedback messages off this class.
        form.classList.add('was-validated');
        if (!form.checkValidity()) return;

        successBox.classList.add('d-none');
        errorBox.classList.add('d-none');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Sending...';

        try {
            var response = await fetch('/api/contact', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: form.Name.value.trim(),
                    email: form.Email.value.trim(),
                    phone: form.Phone.value.trim(),
                    message: form.Message.value.trim()
                })
            });

            if (!response.ok) throw new Error('Request failed with ' + response.status);

            form.reset();
            form.classList.remove('was-validated');
            successBox.classList.remove('d-none');
        } catch (err) {
            // Never silently swallow it — the whole point of this rewrite is that
            // an enquiry either arrives or the sender is told to use email/phone.
            console.error('Contact form submission failed:', err);
            errorBox.classList.remove('d-none');
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Send message';
        }
    });
})();
