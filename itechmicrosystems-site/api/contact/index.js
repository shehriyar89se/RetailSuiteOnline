// POST /api/contact — delivers the website contact form to the company inbox.
//
// The old Razor page logged submissions and returned "message received" without
// sending anything, so enquiries were silently lost. This sends for real via
// SendGrid, and when it can't, it fails loudly so the browser can tell the
// visitor to email or call instead.
//
// Required app settings (Static Web App -> Configuration):
//   SENDGRID_API_KEY    API key with Mail Send permission
//   CONTACT_TO_EMAIL    where enquiries should land (e.g. info@itechmicrosystems.com)
//   CONTACT_FROM_EMAIL  a sender address verified in SendGrid
module.exports = async function (context, req) {
    const body = req.body || {};
    const name = (body.name || '').trim();
    const email = (body.email || '').trim();
    const phone = (body.phone || '').trim();
    const message = (body.message || '').trim();

    // Server-side validation — the browser checks are for UX only, and this
    // endpoint is public, so it can be called directly with anything.
    const errors = [];
    if (!name || name.length > 120) errors.push('name');
    if (!email || email.length > 200 || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) errors.push('email');
    if (phone.length > 40) errors.push('phone');
    if (!message || message.length > 2000) errors.push('message');

    if (errors.length) {
        context.res = { status: 400, jsonBody: { error: 'Invalid fields: ' + errors.join(', ') } };
        return;
    }

    const apiKey = process.env.SENDGRID_API_KEY;
    const toEmail = process.env.CONTACT_TO_EMAIL;
    const fromEmail = process.env.CONTACT_FROM_EMAIL;

    if (!apiKey || !toEmail || !fromEmail) {
        // Log it so the enquiry isn't lost entirely, but still return an error —
        // the visitor must never be told "received" when nothing was delivered.
        context.log.error('Contact form not configured; enquiry not delivered.', { name, email, phone });
        context.res = { status: 500, jsonBody: { error: 'Contact form is not configured.' } };
        return;
    }

    const response = await fetch('https://api.sendgrid.com/v3/mail/send', {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${apiKey}`,
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            personalizations: [{ to: [{ email: toEmail }] }],
            from: { email: fromEmail, name: 'ITECH MICROSYSTEMS website' },
            reply_to: { email: email, name: name },
            subject: `Website enquiry from ${name}`,
            content: [{
                type: 'text/plain',
                value: [
                    `Name:    ${name}`,
                    `Email:   ${email}`,
                    `Phone:   ${phone || '—'}`,
                    '',
                    message
                ].join('\n')
            }]
        })
    });

    if (!response.ok) {
        const detail = await response.text();
        context.log.error(`SendGrid rejected the message (${response.status}): ${detail}`);
        context.res = { status: 502, jsonBody: { error: 'Could not send the message.' } };
        return;
    }

    context.res = { status: 200, jsonBody: { ok: true } };
};
