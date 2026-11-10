// Footer copyright year. The Razor layout rendered this server-side with
// @DateTime.Now.Year; on a static site it has to happen in the browser, so the
// markup ships with the build year as a fallback for anyone without JS.
document.querySelectorAll('[data-current-year]').forEach(function (el) {
    el.textContent = new Date().getFullYear();
});
