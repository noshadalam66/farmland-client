mergeInto(LibraryManager.library, {
  $FarmlandPilotMap: {
    result: 0,
    selected: null,
    dialog: null,
    listener: null,
    marker: null,
    script: null,
    timer: null,
    authFailure: null,
    close: function (result) {
      clearTimeout(this.timer);
      if (this.script) this.script.remove();
      this.script = null;
      if (this.authFailure && window.gm_authFailure === this.authFailure) {
        window.gm_authFailure = this.previousAuthFailure;
      }
      this.authFailure = null;
      if (this.listener) this.listener.remove();
      if (this.marker) this.marker.setMap(null);
      this.listener = null;
      this.marker = null;
      if (this.dialog) this.dialog.remove();
      this.dialog = null;
      this.result = result;
    }
  },

  FarmlandMapOpen__deps: ['$FarmlandPilotMap'],
  FarmlandMapOpen: function (keyPointer, areaKm2) {
    var state = FarmlandPilotMap;
    state.close(0);
    state.selected = null;

    var dialog = document.createElement('dialog');
    dialog.setAttribute('aria-label', 'Choose pilot world center');
    Object.assign(dialog.style, { width: 'min(90vw, 760px)', padding: '16px', color: '#18251c' });
    var instructions = document.createElement('p');
    instructions.textContent = 'Select the center of the terrain area. The outline previews the selected size.';
    var mapElement = document.createElement('div');
    mapElement.style.height = 'min(58vh, 480px)';
    var status = document.createElement('p');
    status.setAttribute('aria-live', 'polite');
    status.textContent = 'Loading map…';
    var confirm = document.createElement('button');
    confirm.textContent = 'Use this location';
    confirm.disabled = true;
    var cancel = document.createElement('button');
    cancel.textContent = 'Cancel';
    dialog.append(instructions, mapElement, status, confirm, cancel);
    document.body.appendChild(dialog);
    state.dialog = dialog;
    cancel.onclick = function () { state.close(2); };
    dialog.addEventListener('cancel', function (event) {
      event.preventDefault();
      state.close(2);
    });
    confirm.onclick = function () {
      if (state.selected) state.close(1);
    };
    var fail = function () {
      if (state.dialog === dialog) state.close(3);
    };
    state.previousAuthFailure = window.gm_authFailure;
    state.authFailure = function () {
      fail();
      if (typeof state.previousAuthFailure === 'function') state.previousAuthFailure();
    };
    window.gm_authFailure = state.authFailure;

    var render = function () {
      if (state.dialog !== dialog) return;
      clearTimeout(state.timer);
      try {
        var map = new google.maps.Map(mapElement, {
          center: { lat: 37.7598, lng: -122.499 },
          zoom: 13,
          streetViewControl: false,
          mapTypeControl: false,
          fullscreenControl: false,
          clickableIcons: false
        });
        state.marker = new google.maps.Rectangle({
          map: map,
          fillColor: '#1978ad',
          fillOpacity: 0.18,
          strokeColor: '#1978ad',
          strokeWeight: 2,
          clickable: false,
          visible: false
        });
        status.textContent = 'No location selected.';
        state.listener = map.addListener('click', function (event) {
          if (!event.latLng) return;
          var lat = event.latLng.lat();
          var lng = event.latLng.lng();
          if (!(lat >= -80 && lat <= 80 && lng >= -180 && lng < 180)) {
            state.selected = null;
            state.marker.setVisible(false);
            confirm.disabled = true;
            status.textContent = 'That location is outside the supported coordinate range.';
            return;
          }
          state.selected = { lat: lat, lng: lng };
          // Approximate the worker's square projected area in geographic coordinates.
          var halfSideDegrees = Math.sqrt(areaKm2) * 500 / 111320;
          var halfWidthDegrees = halfSideDegrees / Math.cos(lat * Math.PI / 180);
          var wrapLongitude = function (degrees) { return (degrees + 540) % 360 - 180; };
          state.marker.setBounds({
            north: lat + halfSideDegrees,
            south: lat - halfSideDegrees,
            west: wrapLongitude(lng - halfWidthDegrees),
            east: wrapLongitude(lng + halfWidthDegrees)
          });
          state.marker.setVisible(true);
          status.textContent = lat.toFixed(6) + ', ' + lng.toFixed(6) + ' · ' + areaKm2 + ' km²';
          confirm.disabled = false;
        });
      } catch (error) {
        fail();
      }
    };

    try {
      dialog.showModal();
      if (window.google && window.google.maps && window.google.maps.Map) {
        render();
        return;
      }
      var script = document.createElement('script');
      script.async = true;
      state.script = script;
      script.src = 'https://maps.googleapis.com/maps/api/js?key=' + encodeURIComponent(UTF8ToString(keyPointer));
      script.onload = render;
      script.onerror = fail;
      state.timer = setTimeout(fail, 15000);
      document.head.appendChild(script);
    } catch (error) {
      fail();
    }
  },

  FarmlandMapRead__deps: ['$FarmlandPilotMap'],
  FarmlandMapRead: function (latitude, longitude) {
    var state = FarmlandPilotMap;
    var result = state.result;
    if (result === 1 && state.selected) {
      HEAPF64[latitude >> 3] = state.selected.lat;
      HEAPF64[longitude >> 3] = state.selected.lng;
    }
    state.result = 0;
    if (result !== 0) state.selected = null;
    return result;
  },

  FarmlandMapClose__deps: ['$FarmlandPilotMap'],
  FarmlandMapClose: function () {
    FarmlandPilotMap.close(0);
    FarmlandPilotMap.selected = null;
  }
});
