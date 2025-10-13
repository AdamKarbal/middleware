class Footer extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: "open" });
    fetch("components/footer.html")
      .then(response => response.text())
      .then(htmlContent => {
        let templateContent = new DOMParser()
          .parseFromString(htmlContent, "text/html")
          .querySelector("template").content;
        let clone = templateContent.cloneNode(true);
        let linkElem = document.createElement("link");
        linkElem.setAttribute("rel", "stylesheet");
        linkElem.setAttribute("href", "/components/footer.css");
        this.shadowRoot.appendChild(linkElem);
        this.shadowRoot.appendChild(clone);
      })
  }
}

customElements.define("mon-footer", Footer);
