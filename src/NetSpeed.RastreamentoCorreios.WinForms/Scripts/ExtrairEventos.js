(() => {
    const historico = document.querySelector('#ver-rastro-unico .ship-steps') ?? document.querySelector('#tabs-rastreamento .ship-steps');
    if (!historico) throw new Error('Histórico não encontrado no DOM.');
    const texto = elemento => {
        const copia = elemento.cloneNode(true);
        copia.querySelectorAll('p, .step-qrcode').forEach(bloco => bloco.append('\n'));
        copia.querySelectorAll('br').forEach(quebra => quebra.replaceWith('\n'));
        return copia.textContent.replace(/[\t\r ]+/g, ' ').replace(/ *\n */g, '\n').trim();
    };
    const padraoData = /^(\d{2})\/(\d{2})\/(\d{4})\s+(\d{2}):(\d{2})(?::(\d{2}))?$/;
    const converterData = valor => {
        const partes = valor.match(padraoData);
        if (!partes) return null;
        const [, dia, mes, ano, hora, minuto, segundo = '00'] = partes;
        const data = new Date(Date.UTC(+ano, +mes - 1, +dia, +hora, +minuto, +segundo));
        if (data.getUTCFullYear() !== +ano || data.getUTCMonth() !== +mes - 1 || data.getUTCDate() !== +dia ||
            data.getUTCHours() !== +hora || data.getUTCMinutes() !== +minuto || data.getUTCSeconds() !== +segundo) return null;
        return `${ano}-${mes}-${dia}T${hora}:${minuto}:${segundo}`;
    };
    const itens = Array.from(historico.children);
    if (itens.some(item => !item.matches('li.step')))
        throw new Error('A estrutura do histórico mudou: existem itens não reconhecidos. A extração foi interrompida para evitar um resultado parcial.');
    const eventos = itens.map(elemento => {
        const conteudo = elemento.querySelector('.step-content');
        if (!conteudo) throw new Error('Um evento não contém .step-content.');
        const paragrafos = Array.from(conteudo.querySelectorAll('p, .text-head, .text-content'))
            .filter(bloco => !bloco.parentElement.closest('p, .text-head, .text-content'));
        const situacao = paragrafos.find(paragrafo => paragrafo.classList.contains('text-head'));
        if (!situacao || !texto(situacao)) throw new Error('Um evento está sem situação.');
        const restantes = paragrafos.filter(paragrafo => paragrafo !== situacao).flatMap(bloco => texto(bloco).split('\n')).filter(Boolean);
        const data = restantes.find(linha => padraoData.test(linha));
        const origem = restantes.find(linha => /^de\s+/i.test(linha));
        const destino = restantes.find(linha => /^para\s+/i.test(linha));
        const locais = paragrafos.filter(paragrafo => paragrafo.classList.contains('text-content'))
            .flatMap(bloco => texto(bloco).split('\n')).filter(linha => linha && !padraoData.test(linha) && linha !== origem && linha !== destino);
        const mensagens = restantes.filter(linha => linha !== data && linha !== origem && linha !== destino && !locais.includes(linha));
        const qr = elemento.querySelector('.step-qrcode');
        if (qr && texto(qr)) mensagens.push(texto(qr));
        return {
            status: texto(situacao), descricao: mensagens.length ? mensagens.join('\n') : null,
            dataHora: data ? converterData(data) : null, local: locais.length ? locais.join('\n') : null,
            origem: origem?.replace(/^de\s+/i, '') ?? null, destino: destino?.replace(/^para\s+/i, '') ?? null,
            mensagensComplementares: mensagens, textoOriginal: texto(elemento),
            links: Array.from(elemento.querySelectorAll('a[href]')).map(link => link.href),
            imagensComplementares: Array.from(elemento.querySelectorAll('.step-content img[src], .step-qrcode img[src]')).map(imagem => imagem.src)
        };
    });
    if (!eventos.length) throw new Error('O histórico não contém eventos reconhecíveis.');
    return JSON.stringify(eventos);
})()
