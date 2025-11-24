let map = null;
let currentMapLayers = []; // Pour garder une trace des anciens tracés

function formatDuration(seconds) {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (days > 0) return `${days} j ${hours} h ${minutes} min`;
  if (hours > 0) return `${hours} h ${minutes} min`;
  return `${minutes} min`;
}

function formatDistance(meters) {
  if (meters >= 1000) {
    return `${(meters / 1000).toFixed(1)} km`;
  }
  return `${meters} m`;
}


function initMap() {
  map = L.map('map').setView([46.603354, 1.888334], 6); // Centre sur la France
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);
}
function clearMap() {
  currentMapLayers.forEach(layer => {
    map.removeLayer(layer);
  });
  currentMapLayers = [];
}

async function startItineraryProcess(startAddress, endAddress) {
  document.getElementById("current-start").textContent = startAddress || "Non défini";
  document.getElementById("current-end").textContent = endAddress || "Non défini";
  document.getElementById("details-container").innerHTML = '<p>Recherche en cours...</p>';
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
    const url = `http://localhost:8000/ServerGPS/itinerary?fromLat=${fromLat}&fromLon=${fromLon}&toLat=${toLat}&toLon=${toLon}`;

    const response = await fetch(url);
    const data = await response.json();

    if (!response.ok) {
      throw new Error(data.error);
    }
    displayInstructions(data.Segments);
    // --- NOUVEAU : afficher distance et durée totales ---
    if (data.TotalDistance && data.TotalDuration) {
      document.getElementById("total-distance").textContent = formatDistance(data.TotalDistance);
      document.getElementById("total-duration").textContent = formatDuration(data.TotalDuration);
    }


    // On force l'utilisation de l'affichage segmenté pour avoir les couleurs
    displayMapRoute(data.Segments);


  } catch (err) {
    console.error("Erreur dans le processus d'itinéraire:", err);
    document.getElementById("details-container").innerHTML = `<p style="color: red;">Erreur: ${err.message}</p>`;
  }
}

async function geocodeAddress(address) {
  const res = await fetch(`https://api-adresse.data.gouv.fr/search/?q=${encodeURIComponent(address)}&limit=1`);
  const data = await res.json();
  if (data.features.length === 0) {
    throw new Error(`Adresse non trouvée : ${address}`);
  }
  return data.features[0].geometry.coordinates;
}

function displayInstructions(segments) {
  const detailsDiv = document.getElementById('details-container');
  detailsDiv.innerHTML = '';

  // 🔧 Ignorer les segments inutiles
  const validSegments = segments.filter(s => s.Distance > 0);

  validSegments.forEach(segment => {
    const icon = (segment.Mode === 'walk') ? '🚶' : '🚲';
    const distanceText = formatDistance(segment.Distance);
    const durationText = formatDuration(segment.Duration);

    const segmentDiv = document.createElement('div');
    segmentDiv.classList.add('segment-block');
    const title = document.createElement('p');
    title.innerHTML = `
      <strong>${icon} ${segment.Mode.toUpperCase()}</strong> :
      ${segment.Instructions.split('→')[0]}
      <br><small>(${distanceText}, environ ${durationText})</small>
    `;
    segmentDiv.appendChild(title);

    const steps = segment.Instructions.split('→').map(s => s.trim()).filter(s => s.length > 0);
    if (steps.length > 1) {
      const stepsList = document.createElement('ul');
      stepsList.classList.add('steps-list');
      steps.forEach(step => {
        const li = document.createElement('li');
        li.textContent = step;
        stepsList.appendChild(li);
      });
      segmentDiv.appendChild(stepsList);
    }

    detailsDiv.appendChild(segmentDiv);
  });
}



// Étape 5 : Dessiner la carte
// Modifiez l'appel dans startItineraryProcess pour n'utiliser QUE celle-ci :
// displayMapRoute(data.Segments); 

function displayMapRoute(segments) {
  // 1. On efface l'ancienne carte
  clearMap();

  let allCoords = []; // Pour centrer la carte à la fin

  segments.forEach(segment => {

    // --- A. GESTION DES COULEURS ---
    let color, weight, dashArray;

    if (segment.Mode === 'bike') {
      // VÉLO = ROUGE, ligne épaisse et pleine
      color = 'red';
      weight = 6;
      dashArray = null;
    } else {
      // MARCHE = BLEU, ligne un peu plus fine, en pointillés
      color = 'blue';
      weight = 5;
      dashArray = null; // Pointillés
    }

    // --- B. DESSIN DU TRACÉ DÉTAILLÉ ---
    // On vérifie si le serveur nous a donné le tracé détaillé (Geometry)
    if (segment.Geometry && segment.Geometry.length > 0) {

      // Conversion des points C# {Latitude, Longitude} -> Leaflet [Lat, Lon]
      const pathCoords = segment.Geometry.map(p => [p.Latitude, p.Longitude]);

      // On dessine la ligne détaillée
      const polyline = L.polyline(pathCoords, {
        color: color,
        weight: weight,
        opacity: 0.8,
        dashArray: dashArray
      });

      polyline.addTo(map);
      currentMapLayers.push(polyline);

      // On ajoute ces points à la liste globale pour le zoom final
      allCoords = allCoords.concat(pathCoords);
    }
    else {
      // SÉCURITÉ : Si pas de géométrie détaillée, on fait une ligne droite (fallback)
      const start = [segment.Start.Latitude, segment.Start.Longitude];
      const end = [segment.End.Latitude, segment.End.Longitude];
      L.polyline([start, end], { color: color, dashArray: dashArray }).addTo(map);
    }
  });

  // --- C. MARQUEURS DÉPART / ARRIVÉE ---
  if (allCoords.length > 0) {
    const startMarker = L.marker(allCoords[0]).bindPopup('Départ');
    const endMarker = L.marker(allCoords[allCoords.length - 1]).bindPopup('Arrivée');

    startMarker.addTo(map);
    endMarker.addTo(map);
    currentMapLayers.push(startMarker);
    currentMapLayers.push(endMarker);

    // On centre la carte pour voir tout le trajet
    map.fitBounds(allCoords);
  }
}
function displayMapPolyline(polylineCoords) {
  clearMap();
  const routePoints = polylineCoords.map(p => [p.Latitude, p.Longitude]);
  const routeLine = L.polyline(routePoints, {
    color: 'blue',
    weight: 4
  });
  routeLine.addTo(map);
  currentMapLayers.push(routeLine);
  if (routePoints.length > 1) {
    const startMarker = L.marker(routePoints[0]).bindPopup('Départ');
    const endMarker = L.marker(routePoints[routePoints.length - 1]).bindPopup('Arrivée');
    startMarker.addTo(map);
    endMarker.addTo(map);
    currentMapLayers.push(startMarker);
    currentMapLayers.push(endMarker);
  }
  map.fitBounds(routeLine.getBounds());
}



document.addEventListener('DOMContentLoaded', () => {
  initMap();
  document.addEventListener('itinerary-search', (event) => {
    console.log("Événement de recherche reçu !", event.detail);
    const { start, end } = event.detail;
    startItineraryProcess(start, end);
  });
  const startAddress = localStorage.getItem("startAddress");
  const endAddress = localStorage.getItem("endAddress");
  if (startAddress && endAddress) {
    console.log("Adresses trouvées dans localStorage, lancement de la recherche...");
    startItineraryProcess(startAddress, endAddress);
  }
});
