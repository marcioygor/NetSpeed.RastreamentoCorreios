NETSPEED - RASTREAMENTO CORREIOS

1. OBJETIVO E FUNCIONALIDADES

Aplicação desktop para automatizar consultas no portal dos Correios, reconhecer o CAPTCHA com OCR local e apresentar o histórico retornado.

- Consulta individual e processamento sequencial de múltiplos códigos.
- Seleção do histórico de cada objeto na aba Eventos.
- Visualização, cópia e exportação do JSON individual ou do lote completo.
- Resumo dos resultados e falhas na aba Objetos.
- Cancelamento, timeouts, até dez tentativas de CAPTCHA e logs de diagnóstico.
- Interrupção do lote quando o portal informa limitação de requisições, preservando os resultados anteriores.

Os eventos mantêm os dados disponíveis no portal: situação, descrição, data/hora, local, origem, destino, mensagens, links, imagens complementares e texto original. Campos ausentes permanecem nulos ou vazios. Um exemplo sintético está em docs\exemplo-resultado.json no código-fonte.

2. TECNOLOGIAS

C# e .NET 8 implementam a aplicação. WinForms oferece a interface desktop e CefSharp incorpora o Chromium para executar o fluxo do portal. Scripts JavaScript consultam os elementos do DOM. ONNX Runtime executa o modelo OCR localmente, sem serviço externo de reconhecimento. System.Text.Json preserva a estrutura dos resultados na exportação. As versões das dependências NuGet estão registradas em packages.lock.json.

3. EXECUTAR A VERSÃO PUBLICADA

Requisitos: Windows x64 compatível, Microsoft Visual C++ Redistributable x64 instalado e acesso à internet.

Baixe NetSpeed.RastreamentoCorreios-win-x64.zip na Release v1.0.0 do repositório https://github.com/marcioygor/NetSpeed.RastreamentoCorreios. Extraia o ZIP inteiro para uma pasta local e execute NetSpeed.RastreamentoCorreios.exe. Não execute de dentro do ZIP nem copie apenas o executável: mantenha as bibliotecas, recursos do Chromium, pasta ModelosOcr e appsettings.json juntos.

A publicação self-contained inclui o runtime .NET 8. Não é necessário instalar o runtime .NET, o SDK ou o Visual Studio para utilizar essa versão.

4. COMPILAR E TESTAR O CÓDIGO-FONTE

Requisitos: Windows x64, SDK .NET 8, Microsoft Visual C++ Redistributable x64 e acesso à internet para restaurar os pacotes NuGet. O Visual Studio com suporte a WinForms em .NET 8 é opcional para edição e depuração.

Na raiz do repositório:

dotnet restore .\NetSpeed.RastreamentoCorreios.sln --locked-mode
dotnet build .\NetSpeed.RastreamentoCorreios.sln -c Release --no-restore
dotnet test .\NetSpeed.RastreamentoCorreios.sln -c Release --no-build --no-restore
dotnet run --project .\src\NetSpeed.RastreamentoCorreios.WinForms -c Release --no-build

Feche a aplicação antes de recompilar a mesma configuração. Para gerar a publicação completa:

dotnet publish .\src\NetSpeed.RastreamentoCorreios.WinForms\NetSpeed.RastreamentoCorreios.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -o .\entrega\v1.0.0\win-x64

Os arquivos de publicação e ZIPs de entrega não devem ser versionados na branch main; o ZIP executável é distribuído como anexo da Release.

5. UTILIZAÇÃO, CONFIGURAÇÃO E LOGS

Informe um código e clique em Consultar. Para um lote, separe os códigos por ponto e vírgula, vírgula ou espaço. A execução é sequencial; códigos repetidos reaproveitam apenas resultados concluídos no mesmo lote. Uma nova consulta busca os dados novamente.

As abas Eventos e JSON oferecem seletores independentes quando há múltiplos objetos. Copiar JSON e Exportar JSON utilizam exatamente o conteúdo exibido na aba JSON. Cancelar interrompe as esperas e impede novas tentativas, preservando os objetos já tratados no lote. Uma requisição já recebida pelo portal não pode ser desfeita.

O appsettings.json ao lado do executável configura URL, timeouts, OCR e tentativas. Os padrões são 45 segundos para navegação, 20 segundos para DOM e OCR, e dez tentativas de CAPTCHA.

Logs: %LOCALAPPDATA%\NetSpeed\RastreamentoCorreios\logs

A interface apresenta mensagens objetivas; os arquivos de log mantêm os detalhes técnicos das exceções. Os logs não fazem parte do ZIP de distribuição.

6. LIMITAÇÕES CONHECIDAS

O portal pode restringir a frequência das consultas. Ao detectar uma mensagem explícita de excesso de requisições ou HTTP 429 disponível, a aplicação interrompe novas consultas do lote e marca os objetos restantes como não consultados. Não há repetição automática após esse bloqueio nem frequência segura confirmada pelo portal.

Mudanças no portal podem exigir revisão dos seletores. O OCR não garante reconhecimento de todas as imagens; sua confiança interna não representa uma taxa de acerto independente. A execução em outro computador e diferentes escalas de DPI ainda depende de validação nesse ambiente.
