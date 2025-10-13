const mobileMenu = document.getElementById('mobileMenu');
const navLinks = document.querySelector('nav .links');
mobileMenu.addEventListener('click', () => {
  if (mobileMenu.classList.contains('openBurger')) {
    mobileMenu.classList.remove('openBurger');
    mobileMenu.classList.add('closeBurger');
    navLinks.classList.remove('open');
  } else {
    mobileMenu.classList.remove('closeBurger');
    mobileMenu.classList.add('openBurger');
    navLinks.classList.add('open');
  }
});
