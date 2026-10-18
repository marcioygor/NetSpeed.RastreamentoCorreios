(() => {
    const erro = mensagem => JSON.stringify({
        estado: /captcha\s*(?:est[aá]\s*)?(?:inv[aá]lido|incorreto)/i.test(mensagem) ? 'captcha-invalido' : 'erro',
        mensagem
    });
    const captcha = document.querySelector('#captcha');
    const mensagemCaptcha = captcha?.validationMessage ?? '';
    if (mensagemCaptcha.trim()) return erro(mensagemCaptcha);
    const objeto = document.querySelector('#objeto');
    if (objeto?.validationMessage.trim()) return erro(objeto.validationMessage);
    const alerta = document.querySelector('#alerta.aberto .msg')?.textContent.trim();
    if (alerta) return /^buscando[.\s…]*$/i.test(alerta) ? null : erro(alerta);
    const historico = document.querySelector('#ver-rastro-unico .ship-steps') ?? document.querySelector('#tabs-rastreamento .ship-steps');
    if (historico?.children.length) return JSON.stringify({ estado: 'eventos', mensagem: null });
    return null;
})()
