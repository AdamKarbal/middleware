class BurgerMenu extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: "open" });
    fetch("components/burgerMenu.html")
      .then(response => response.text())
      .then(htmlContent => {
        let templateContent = new DOMParser()
          .parseFromString(htmlContent, "text/html")
          .querySelector("template").content;
        let clone = templateContent.cloneNode(true);
        let linkElem = document.createElement("link");
        linkElem.setAttribute("rel", "stylesheet");
        linkElem.setAttribute("href", "/components/burgerMenu.css");
        this.shadowRoot.appendChild(linkElem);
        this.shadowRoot.appendChild(clone);
        this.initBurgerMenu();
      })
      .catch(error => {
        console.error('Error loading burger menu:', error);
      });;
  }

  initBurgerMenu() {
    const shadow = this.shadowRoot;
    const mobileMenu = shadow.querySelector("#mobileMenu");
    const links = shadow.querySelector(".links");
    mobileMenu.addEventListener("click", () => {
      mobileMenu.classList.toggle("openBurger");
      links.classList.toggle("open");
    });
  }
}

customElements.define("mon-burgermenu", BurgerMenu);
