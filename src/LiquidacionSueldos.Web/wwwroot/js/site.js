// Pequeñas mejoras de interfaz. Todo funciona también sin JavaScript, salvo lo indicado.
document.addEventListener('DOMContentLoaded', function () {
  // Confirmaciones: <button data-confirm="¿Seguro?">
  document.querySelectorAll('[data-confirm]').forEach(function (el) {
    el.addEventListener('click', function (ev) {
      if (!window.confirm(el.getAttribute('data-confirm'))) ev.preventDefault();
    });
  });
  // Impresión: <button data-print>
  document.querySelectorAll('[data-print]').forEach(function (el) {
    el.addEventListener('click', function () { window.print(); });
  });
});
