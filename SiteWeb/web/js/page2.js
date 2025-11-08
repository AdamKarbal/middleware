let map = null;
let currentMapLayers = []; // Pour garder une trace des anciens tracés

// Étape 1 : Initialiser la carte (ne change pas)
function initMap() {
  map = L.map('map').setView([46.603354, 1.888334], 6); // Centre sur la France
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);
}

// Étape 2 : Fonction pour nettoyer la carte (NOUVEAU)
function clearMap() {
  currentMapLayers.forEach(layer => {
    map.removeLayer(layer);
  });
  currentMapLayers = [];
}

// Étape 3 : La logique principale (maintenant avec des arguments)
async function startItineraryProcess(startAddress, endAddress) {

  // Mettre à jour la sidebar
  document.getElementById("current-start").textContent = startAddress || "Non défini";
  document.getElementById("current-end").textContent = endAddress || "Non défini";
  document.getElementById("details-container").innerHTML = '<p>Recherche en cours...</p>';

  // Nettoyer l'ancienne route avant de chercher la nouvelle
  clearMap();

  try {
    // b. Convertir les adresses en Coordonnées (Lat/Lon)
    const [startCoords, endCoords] = await Promise.all([
      geocodeAddress(startAddress),
      geocodeAddress(endAddress)
    ]);

    const fromLat = startCoords[1];
    const fromLon = startCoords[0];
    const toLat = endCoords[1];
    const toLon = endCoords[0];

    // c. Appeler votre GPSserver
    const url = `http://localhost:8000/ServerGPS/itinerary?fromLat=${fromLat}&fromLon=${fromLon}&toLat=${toLat}&toLon=${toLon}`;

    const response = await fetch(url);
    if (!response.ok) {
      // Gérer les erreurs de réseau (serveur éteint, etc.)
      throw new Error(`Erreur réseau: ${response.statusText}`);
    }
    const data = await response.json();

    if (data.error) {
      // Gérer les erreurs de votre API (pas de station, etc.)
      throw new Error(data.error);
    }

    // Sauver la recherche réussie dans localStorage
    localStorage.setItem("startAddress", startAddress);
    localStorage.setItem("endAddress", endAddress);

    // d. Afficher les résultats
    displayInstructions(data.segments);
    displayMapRoute(data.segments);

  } catch (err) {
    console.error("Erreur dans le processus d'itinéraire:", err);
    document.getElementById("details-container").innerHTML = `<p style="color: red;">Erreur: ${err.message}</p>`;
   }
}

// Helper pour géocoder (ne change pas)
async function geocodeAddress(address) {
  const res = await fetch(`https://api-adresse.data.gouv.fr/search/?q=${encodeURIComponent(address)}&limit=1`);
  const data = await res.json();
  if (data.features.length === 0) {
    throw new Error(`Adresse non trouvée : ${address}`);
  }
  return data.features[0].geometry.coordinates;
}

// Étape 4 : Afficher les instructions (ne change pas)
function displayInstructions(segments) {
  const detailsDiv = document.getElementById('details-container');
  detailsDiv.innerHTML = '';
  segments.forEach(segment => {
    const icon = (segment.Mode === 'walk') ? '🚶' : '🚲';
    const duration = Math.round(segment.Duration / 60);
    const distance = Math.round(segment.Distance);
    const p = document.createElement('p');
    p.innerHTML = `<strong>${icon} ${segment.Mode.toUpperCase()}</strong>: ${segment.Instructions} 
                       <br><small>(${distance}m, environ ${duration} min)</small>`;
    detailsDiv.appendChild(p);
  });
}

// Étape 5 : Dessiner la carte (légèrement modifié pour le nettoyage)
function displayMapRoute(segments) {
  let allCoords = [];
  segments.forEach(segment => {
    const startCoords = [segment.Start.latitude, segment.Start.longitude];
    const endCoords = [segment.End.latitude, segment.End.longitude];
    allCoords.push(startCoords);
    allCoords.push(endCoords);

    let color = (segment.Mode === 'walk') ? 'blue' : 'green';
    let dash = (segment.Mode === 'walk') ? '5, 5' : null;

    // On ajoute les lignes au tableau pour pouvoir les supprimer plus tard
    const polyline = L.polyline([startCoords, endCoords], {
      color: color,
      dashArray: dash
    });
    currentMapLayers.push(polyline);
    polyline.addTo(map);
  });

  // On ajoute aussi les marqueurs au tableau
  const startMarker = L.marker(allCoords[0]).bindPopup('Départ');
  const endMarker = L.marker(allCoords[allCoords.length - 1]).bindPopup('Arrivée');
  currentMapLayers.push(startMarker);
  currentMapLayers.push(endMarker);
  startMarker.addTo(map);
  endMarker.addTo(map);

  map.fitBounds(allCoords); // Centre la carte
}

// --- ÉTAPE 6 : Le chef d'orchestre (MODIFIÉ) ---
document.addEventListener('DOMContentLoaded', () => {
  // Initialiser la carte
  initMap();

  // Écouter les clics sur le bouton "Démarrer" (via l'événement)
  document.addEventListener('itinerary-search', (event) => {
    console.log("Événement de recherche reçu !", event.detail);
    const { start, end } = event.detail;
    startItineraryProcess(start, end);
  });

  // Lancer une recherche initiale si des adresses sont en mémoire
  const startAddress = localStorage.getItem("startAddress");
  const endAddress = localStorage.getItem("endAddress");
  if (startAddress && endAddress) {
    console.log("Adresses trouvées dans localStorage, lancement de la recherche...");
    startItineraryProcess(startAddress, endAddress);
  }
});
