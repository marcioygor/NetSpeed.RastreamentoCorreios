(() => {
    const texto = document.body?.innerText ?? '';
    const mensagem = texto.match(/(?:We are sorry,?\s*but\s*)?(?:the site has received\s+)?too many requests(?:[.!]?\s*Please try again later[.!]?)?|excesso de (?:requisições|requisicoes)|limite de (?:requisições|requisicoes|consultas) (?:atingido|excedido)|muitas (?:requisições|requisicoes|solicitações|solicitacoes)/i);
    if (mensagem) return mensagem[0];
    const respostaLimitada = performance.getEntriesByType('resource').find(entrada =>
        (entrada.initiatorType === 'fetch' || entrada.initiatorType === 'xmlhttprequest') &&
        entrada.responseStatus === 429 && new URL(entrada.name, location.href).origin === location.origin);
    return respostaLimitada ? `HTTP 429 em ${respostaLimitada.name}` : null;
})()
