(() => {
    const imagem = document.querySelector('#captcha_image');
    if (!imagem) throw new Error('O elemento #captcha_image não foi encontrado no portal.');
    if (!imagem.complete || !imagem.naturalWidth || imagem.currentSrc === {{origemAnterior}}) return null;
    const canvas = document.createElement('canvas');
    canvas.width = imagem.naturalWidth;
    canvas.height = imagem.naturalHeight;
    canvas.getContext('2d').drawImage(imagem, 0, 0);
    return JSON.stringify({ origem: imagem.currentSrc, conteudoBase64: canvas.toDataURL('image/png').split(',')[1] });
})()