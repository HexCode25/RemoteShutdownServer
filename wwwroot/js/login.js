document.addEventListener('DOMContentLoaded', function () {
  const input = document.getElementById('password');
  if (input) input.focus();
  // Enter key submit support
  input && input.addEventListener('keypress', function (event) {
    if (event.key === 'Enter') event.target.form.submit();
  });
});
