class Itineraire extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: "open" });
    fetch("components/itineraire.html")
      .then(response => response.text())
      .then(htmlContent => {
        let templateContent = new DOMParser()
          .parseFromString(htmlContent, "text/html")
          .querySelector("template").content;
        let clone = templateContent.cloneNode(true);
        let linkElem = document.createElement("link");
        linkElem.setAttribute("rel", "stylesheet");
        linkElem.setAttribute("href", "/components/itineraire.css");
        this.shadowRoot.appendChild(linkElem);
        this.shadowRoot.appendChild(clone);
        this.setupAutocomplete();
      });
  }
  setupAutocomplete() {
    const startInput = this.shadowRoot.querySelector("#start");
    const endInput = this.shadowRoot.querySelector("#end");
    const startSuggestions = this.shadowRoot.querySelector("#start-suggestions");
    const endSuggestions = this.shadowRoot.querySelector("#end-suggestions");
    const startButton = this.shadowRoot.querySelector("#start-itinerary");
    let startAddress = null;
    let endAddress = null;
    const debounce = (fn, delay) => {
      let timeout;
      return (...args) => {
        clearTimeout(timeout);
        timeout = setTimeout(() => fn(...args), delay);
      };
    };
    const fetchSuggestions = async (query, suggestionList, onSelect) => {
      if (!query.trim()) {
        suggestionList.innerHTML = "";
        suggestionList.style.display = "none";
        return;
      }
      try {
        const res = await fetch(`https://api-adresse.data.gouv.fr/search/?q=${encodeURIComponent(query)}&limit=5`);
        const data = await res.json();
        suggestionList.innerHTML = "";
        data.features.forEach(feature => {
          const li = document.createElement("li");
          li.textContent = feature.properties.label;
          li.addEventListener("click", () => {
            onSelect(feature.properties.label);
            suggestionList.innerHTML = "";
            suggestionList.style.display = "none";
          });
          suggestionList.appendChild(li);
        });
        suggestionList.style.display = data.features.length > 0 ? "block" : "none";
      } catch (err) {
        console.error("Erreur lors de la récupération des suggestions :", err);
      }
    };
    const debouncedFetchStart = debounce(query =>
      fetchSuggestions(query, startSuggestions, value => {
        startInput.value = value;
        startAddress = value;
      }), 300);
    const debouncedFetchEnd = debounce(query =>
      fetchSuggestions(query, endSuggestions, value => {
        endInput.value = value;
        endAddress = value;
      }), 300);
    startInput.addEventListener("input", e => debouncedFetchStart(e.target.value));
    endInput.addEventListener("input", e => debouncedFetchEnd(e.target.value));
    startButton.addEventListener("click", () => {
      if (startAddress && endAddress) {
        const searchEvent = new CustomEvent('itinerary-search', {
          detail: {
            start: startAddress,
            end: endAddress
          },
          bubbles: true, 
          composed: true 
        });
        this.dispatchEvent(searchEvent);
      }
    });
  }
}

customElements.define("mon-itineraire", Itineraire);
