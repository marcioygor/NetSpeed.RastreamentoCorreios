1. SOBRE O PROJETO

A aplicação automatiza consultas de rastreamento no portal dos Correios, reconhece o CAPTCHA localmente e apresenta todo o histórico retornado na interface e em JSON.

2. TECNOLOGIAS UTILIZADAS

C# e .NET 8 implementam a aplicação e a automação. Windows Forms oferece a interface desktop. CefSharp incorpora o Chromium para executar o fluxo real do portal. JavaScript localiza e interpreta elementos do DOM. ONNX Runtime executa o modelo OCR local, sem serviço externo de reconhecimento. As dependências NuGet estão declaradas nos projetos, com versões registradas em packages.lock.json.

A navegação aguarda o carregamento e os estados esperados da página. Os scripts usam seletores dos campos, CAPTCHA, alertas e histórico; o histórico completo tem preferência sobre o resumo. O OCR usa pré-processamento de imagem e decodificação CTC. O resultado é validado pela resposta do portal, com renovação da imagem e até dez tentativas. A lógica de navegação, extração e reconhecimento permanece separada para facilitar manutenção e testes.

Os eventos são convertidos em modelos C# e serializados com System.Text.Json. São preservados ordem, situação, descrição, data/hora, local, origem, destino, mensagens, links, imagens complementares e texto original. Campos ausentes permanecem nulos ou vazios; datas não recebem um fuso que o portal não informa.

3. COMO EXECUTAR

Pré-requisitos: Windows x64, SDK .NET 8, Visual Studio com suporte a Windows Forms em .NET 8 e Visual C++ Redistributable x64. É necessário acesso à internet para restaurar os pacotes NuGet e consultar o portal.

Abra NetSpeed.RastreamentoCorreios.sln no Visual Studio, restaure os pacotes da solução, selecione Release e inicie o projeto NetSpeed.RastreamentoCorreios.WinForms. Feche a instância da mesma configuração antes de recompilar.

Na raiz da solução, também é possível executar:
dotnet restore .\NetSpeed.RastreamentoCorreios.sln --locked-mode
dotnet build .\NetSpeed.RastreamentoCorreios.sln -c Release --no-restore
dotnet test .\NetSpeed.RastreamentoCorreios.sln -c Release --no-build --no-restore
dotnet run --project .\src\NetSpeed.RastreamentoCorreios.WinForms -c Release --no-build

A publicação fica em entrega\win-x64: copie a pasta inteira e execute NetSpeed.RastreamentoCorreios.exe. Ela inclui o runtime .NET 8 e continua exigindo Visual C++ Redistributable x64. Para gerar a publicação:
dotnet publish .\src\NetSpeed.RastreamentoCorreios.WinForms\NetSpeed.RastreamentoCorreios.WinForms.csproj -c Release -r win-x64 --self-contained true -o .\entrega\win-x64

O appsettings.json ao lado do executável configura URL, timeouts, OCR e tentativas. Os padrões são 45 segundos para navegação, 20 segundos para DOM e OCR, e dez tentativas de CAPTCHA. Configurações inválidas impedem a inicialização com mensagem e log.

4. COMO UTILIZAR

Informe um código, como NN437873753BR, e clique em Consultar. Aguarde e veja o histórico na aba Eventos. Use Copiar JSON ou Exportar JSON para obter o resultado. Para um lote, separe os códigos por ponto e vírgula, vírgula ou espaço; a execução é sequencial. A aba Objetos permite selecionar os resultados. Um exemplo de JSON com dados sintéticos está em docs\exemplo-resultado.json.

Cancelar interrompe as esperas e impede novas tentativas locais, preservando os objetos já tratados no lote. Uma solicitação já recebida pelo portal não pode ser desfeita. Código inválido, objeto não encontrado, erro de navegação, mudança de elementos, timeout e falha de processamento são informados; no lote, cada objeto mantém seu resultado ou erro. Estruturas desconhecidas interrompem a extração para evitar histórico parcial.

A divisória entre navegador e resultados pode ser arrastada para ajustar os painéis.

Os logs ficam em %LOCALAPPDATA%\NetSpeed\RastreamentoCorreios\logs e as mensagens aparecem no painel de atividade.

Limitações: alterações no portal podem exigir revisão dos seletores; a confiança do OCR não equivale a uma taxa de acerto independente. Não há garantia de reconhecimento em todas as imagens. A distribuição em outro computador, diferentes escalas de DPI e o esgotamento do timeout nativo do OCR ainda precisam de validação. Melhorias possíveis são ampliar as amostras independentes do OCR e as regressões com históricos autorizados distintos.
