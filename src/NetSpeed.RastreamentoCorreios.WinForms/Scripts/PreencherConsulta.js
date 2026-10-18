(() => {
    const objeto = document.querySelector('#objeto');
    const captcha = document.querySelector('#captcha');
    const imagem = document.querySelector('#captcha_image');
    const pesquisar = document.querySelector('#b-pesquisar');
    if (!objeto || !captcha || !imagem || !pesquisar) throw new Error('O formulário de consulta mudou.');
    if (!imagem.complete || imagem.currentSrc !== {{origemImagem}}) throw new Error('A imagem do CAPTCHA mudou durante o reconhecimento.');
    objeto.value = {{codigo}};
    objeto.dispatchEvent(new Event('input', { bubbles: true }));
    objeto.dispatchEvent(new Event('change', { bubbles: true }));
    captcha.value = {{captcha}};
    captcha.setCustomValidity('');
    captcha.dispatchEvent(new Event('input', { bubbles: true }));
    captcha.dispatchEvent(new Event('change', { bubbles: true }));
    pesquisar.click();
    return JSON.stringify(true);
})()