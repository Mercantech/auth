window.mercAuth = window.mercAuth || {};
window.mercAuth.copyText = async function (text) {
  await navigator.clipboard.writeText(text);
};
