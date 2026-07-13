// ==UserScript==
// @name         Kommo · Enter para enviar
// @namespace    quickreply.kommo.enter
// @version      1.0
// @description  En Kommo: Enter envía el mensaje (reusa el atajo nativo Ctrl+Enter). Shift+Enter sigue siendo salto de línea.
// @author       QuickReply
// @match        *://*.kommo.com/*
// @match        *://*.amocrm.com/*
// @match        *://*.amocrm.ru/*
// @run-at       document-start
// @grant        none
// ==/UserScript==

(function () {
  'use strict';

  // ¿El foco está en el cuadro de mensaje? (en Kommo es un contenteditable; a veces textarea)
  function esCuadroMensaje(el) {
    if (!el) return false;
    return el.isContentEditable === true || el.tagName === 'TEXTAREA';
  }

  // ¿Hay un menú/lista de sugerencias abierto? (plantillas "/", menciones "@", emojis, etc.)
  // Si lo hay, dejamos que Enter elija la opción en vez de enviar.
  function haySugerenciaAbierta() {
    const sel = '[role="listbox"],[role="menu"],[role="grid"],[role="option"],.tt-menu,.suggest,.emoji-mart';
    for (const el of document.querySelectorAll(sel)) {
      if (el.offsetParent !== null) return true; // visible en pantalla
    }
    return false;
  }

  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter') return;
    if (e.shiftKey || e.ctrlKey || e.metaKey || e.altKey) return; // Shift/Ctrl+Enter: no tocar
    if (e.isComposing) return;                                    // respetar acentos / IME
    if (!esCuadroMensaje(e.target)) return;
    if (haySugerenciaAbierta()) return;                           // dejar elegir en los menús

    // Cancelamos el "salto de línea" y disparamos el envío nativo (Ctrl+Enter)
    e.preventDefault();
    e.stopPropagation();
    e.stopImmediatePropagation();

    const opts = {
      key: 'Enter', code: 'Enter', keyCode: 13, which: 13,
      ctrlKey: true, bubbles: true, cancelable: true
    };
    e.target.dispatchEvent(new KeyboardEvent('keydown', opts));
    e.target.dispatchEvent(new KeyboardEvent('keyup', opts));
  }, true); // fase de captura: corremos antes que el manejador de Kommo
})();
