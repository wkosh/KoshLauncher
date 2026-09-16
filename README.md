# KoshLauncher

<img width="1180" height="760" alt="9108" src="https://github.com/user-attachments/assets/fe1cdb10-dda5-4361-bb5a-a0990c3f6238" />


> **Edição pública com código-fonte ofuscado.** Os identificadores internos foram renomeados e o C# compactado. Esta cópia compila normalmente; nomes exigidos pelo XAML, JSON e testes foram preservados. O código original legível não faz parte deste pacote. Ofuscação dificulta a leitura, mas a lógica continua acessível a quem recebe o código-fonte.

Launcher de Minecraft Java para Windows, feito em C# / WPF / .NET 10, com identidade vampiresca em preto e vermelho.

## Publicação protegida (0.4.2)

O perfil `Protected` usa Obfuscar 2.2.50 para renomear tipos/membros e ocultar strings do assembly próprio antes de empacotar o EXE. Não exige licença, conexão de licenciamento ou mudança na conta. A interface WPF e o contrato JSON das configurações têm exclusões de compatibilidade. Bibliotecas de terceiros não são modificadas.

Para gerar e testar a distribuição protegida no Windows:

    powershell -NoProfile -ExecutionPolicy Bypass -File build/Publish-Protected.ps1

O EXE fica em `artifacts/protected`. Cada execução usa diretórios de compilação novos. O mapa de nomes, a configuração da ferramenta e os assemblies originais ficam apenas em `artifacts/protection-builds`, ignorado pelo Git; não os distribua junto do EXE. O script interrompe a entrega se a ofuscação ou os testes falharem. O perfil `SingleFile` continua disponível para uma publicação sem ofuscação e o F5 continua normal para desenvolvimento.

Esta proteção aumenta o esforço de análise, mas não impede engenharia reversa, alteração do executável ou extração de dados em memória. Não é criptografia de segredos nem DRM. Código-fonte publicado num repositório público continua legível. A ofuscação não substitui assinatura de código; esta versão não recebe certificado de assinatura.

Referência da ferramenta: https://docs.lextudio.com/obfuscar/getting-started/configuration

## Novidades da versão 0.4.0

- Interface atualizada: campos e listas escuros, navegação com ícones, marca vampiresca, barra de jogo compacta e cartões de gráficos/personagem.
- OptiFine: consulta edições no site oficial, filtra prévias e permite instalar pelo launcher ou importar um JAR original. Identifica a versão base, prepara o Java e chama o instalador oficial sem abrir a janela externa. Instala em uma pasta temporária e publica o perfil ao concluir. Não combina OptiFine com Fabric automaticamente.
- Skins: escolhe PNG 64×64 ou clássico 64×32, mostra prévia do rosto e permite aplicar o modelo clássico/slim à conta Microsoft conectada. A alteração só é enviada quando o usuário clica em Aplicar à conta Microsoft. Sessão local não aplica skin ao jogo sem integração adicional.
- Testes de PNG inválido, dimensões de skin, bloqueio de envio sem conta e incompatibilidade de versão do instalador OptiFine.

Instalação real do OptiFine 1.20.1 HD U I6 e leitura do perfil pelo CmlLib verificadas em pasta isolada. Outras edições dependem da compatibilidade do instalador oficial. O envio real de skin e a entrada no jogo com conta Microsoft ainda precisam de validação com a conta do usuário.

O executável inclui somente a ponte própria `Helpers/KoshOptifineBridge.class`; o OptiFine é obtido em tempo de uso do site oficial. O código da ponte está junto do projeto. Para recompilar após alterá-lo, use um JDK com `javac --release 8 KoshLauncher/Helpers/KoshOptifineBridge.java`.

## Funcionalidades da versão 0.2.0

- Seleção de versões Vanilla e perfis locais, incluindo Fabric.
- Criação de perfis Fabric na pasta do launcher.
- Instalação/verificação, progresso por etapa e volume transferido; cancelamento da preparação.
- Sessão local por nick e integração Microsoft com entrada interativa.
- Sessões Microsoft protegidas para o usuário atual do Windows (DPAPI).
- Uma pasta de mundos/mods/configurações por identificador de versão.
- RAM, resolução, tela cheia, filtro de versões e neon configuráveis.
- Importação de arquivos de mods sem sobrescrever arquivos existentes.
- Backup ZIP dos mundos, com escolha do destino.
- Captura de saída e erros por execução; diagnóstico básico de falhas.
- Lista e resumo de perfis locais; seleção local disponível quando a consulta online falha.

## Executar no Visual Studio

1. Instale Visual Studio 2026 com Desenvolvimento para desktop com .NET.
2. Abra KoshLauncher.slnx.
3. Restaure os pacotes e pressione F5.

Pelo terminal:

    dotnet build KoshLauncher/KoshLauncher.csproj
    dotnet run --project KoshLauncher/KoshLauncher.csproj

## Uso

Selecione uma versão, informe um nick local ou entre com Microsoft e clique em Jogar.
Em Instalações, selecione uma versão Vanilla estável para Adicionar Fabric.
O perfil Fabric passa a aparecer na lista. Jogar prepara as dependências restantes.
Importe mods adequados para a versão e o loader; o importador copia os arquivos escolhidos e não resolve dependências.
Use Configurações para salvar RAM, resolução, tela cheia, neon e filtro da lista.
Feche o jogo antes de importar mods ou criar backup; o launcher bloqueia essas operações enquanto sua execução está ativa.

## Publicar como um único executável

    dotnet publish KoshLauncher/KoshLauncher.csproj -p:PublishProfile=SingleFile -o artifacts/singlefile

O perfil SingleFile gera um EXE Windows x64 comprimido, incluindo o .NET e as licenças.
Arquivos de suporte são extraídos automaticamente na área temporária do usuário durante a execução.
Os dados do Minecraft continuam na pasta de dados descrita abaixo. Não é necessário distribuir as DLLs separadamente.
No Visual Studio, selecione o perfil SingleFile na tela Publicar.

## Dados do aplicativo

Tudo fica em %LOCALAPPDATA%\KoshLauncher:

- Minecraft: versões, bibliotecas e recursos compartilhados.
- Instances/<perfil>: mundos, mods e configuração do jogo.
- Instances/<perfil>/launcher-logs/<data>: saída e erros da execução.
- settings.json: preferências.
- accounts.protected: sessão Microsoft criptografada pelo Windows.

Mundos antigos em Minecraft/saves não são movidos automaticamente.
Crie backup e copie os mundos desejados para Instances/<perfil>/saves com o jogo fechado.
Atualizar o loader Fabric cria outro identificador e, portanto, outra pasta de dados.

## Validação

    dotnet run --project tests/KoshLauncher.SmokeTests/KoshLauncher.SmokeTests.csproj

Verifica o resumo Vanilla/Fabric, bloqueio de controles, ativação do neon,
compatibilidade/serialização de configurações e criptografia de contas.
Também renderiza as páginas nos tamanhos padrão e mínimo para inspeção.
Os testes não entram em uma conta Microsoft nem iniciam um jogo real.

## Limites desta versão

- Integração Microsoft implementada; o fluxo completo precisa ser validado com uma conta que tenha acesso ao Minecraft Java e pode depender do WebView2 e da disponibilidade dos serviços.
- Perfis encontrados significam JSON local; não são garantia de download completo. Jogar verifica/prepara os arquivos.
- Cancelar preserva arquivos concluídos. Arquivos incompletos podem ser baixados novamente.
- Importação de mods não valida compatibilidade nem instala Fabric API automaticamente.
- Uma instância por identificador de versão; ainda não há várias instâncias nomeadas para o mesmo perfil.
- Ainda não há catálogo de modpacks, Forge, atualização automática ou instalador assinado.
- Resolução configura a janela; tela cheia também depende do jogo e do monitor.
- Seleção de perfis locais sem rede não garante execução offline de toda versão.

## Créditos

Base de aprendizado: MatheusTGP, “Fiz o Minecraft abrir sem launcher (e deu certo)”:
https://www.youtube.com/watch?v=DLQIu8JBjws

Bibliotecas: CmlLib.Core e CmlLib.Core.Auth.Microsoft.
Fonte: Pirata One; licença OFL em KoshLauncher/Assets/Fonts/OFL.txt.
A referência de organização visual foi o SKLauncher; a marca e interface Kosh são próprias.
Projeto independente, sem afiliação à Mojang, Microsoft, Fabric ou outros launchers.

# KoshLauncher by: wkosh
