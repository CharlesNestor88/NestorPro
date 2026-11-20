# Compilar e testar o código

O código-fonte do NestorPro DIRETO está disponível gratuitamente sob a licença MIT. Os componentes externos mantêm suas próprias licenças.

## Ferramentas

- Windows x64 para compilar e executar a interface Windows Forms.
- .NET SDK 10.
- Acesso ao NuGet para restaurar Nefarius.ViGEm.Client e Spectre.Console.
- Interception e ViGEmBus para executar a captura e criar o controle virtual. Os testes puros não instalam nem precisam desses drivers.

## Obter o projeto

Baixe o ZIP pelo botão Code do GitHub e extraia, ou use:

```powershell
git clone https://github.com/CharlesNestor88/NestorPro.git
Set-Location "NestorPro"
```

## Compilar sem instalar drivers

```powershell
dotnet restore ".\src\NestorPro\KeyboardToGamepad.csproj"
dotnet build ".\src\NestorPro\KeyboardToGamepad.csproj" -c Release --no-restore
dotnet run --project ".\tests\NestorPro.Tests\Tests.csproj" -c Release
```

Isso valida a compilação e os testes puros. Sem os drivers, o painel pode abrir e indicar falha de inicialização da captura.

## Preparar a biblioteca nativa

Revise a licença do Interception e execute opcionalmente:

```powershell
& ".\scripts\Get-Interception.ps1"
```

O script baixa a versão fixada 1.0.1 da origem oficial, confere o SHA256 do arquivo e copia a DLL x64 e o instalador para a pasta native do projeto. Não instala drivers, não altera o registro e não reinicia o PC. Esses binários ficam ignorados pelo Git.

O download não transfere direitos de uso comercial; veja THIRD-PARTY-NOTICES.md.

## Executar a interface

Após instalar os drivers pelas instruções oficiais e reiniciar quando necessário:

```powershell
dotnet run --project ".\src\NestorPro\KeyboardToGamepad.csproj" -c Release
```

O modo padrão é o painel NestorPro DIRETO. O nome técnico do executável continua CharlesDS4-SomenteDS4 por compatibilidade com a instalação existente; o fluxo padrão não inicia DS4Windows.

O arquivo config.json corresponde ao modo console legado. O painel usa CharlesSettings e grava os ajustes na pasta de dados local do usuário. Nenhum ajuste pessoal do desenvolvedor foi incluído.

## Gerar um executável autocontido

```powershell
dotnet publish ".\src\NestorPro\KeyboardToGamepad.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\artifacts\NestorPro"
```

Se a DLL estiver em native na hora da compilação, o projeto a inclui conforme o csproj. Isso não elimina a necessidade de instalar o driver no computador de destino.

O botão Instalar driver do painel procura Drivers/install-interception.exe ao lado do executável. Para preparar essa pasta em uma distribuição cuja licença permita incluir o instalador:

```powershell
New-Item -ItemType Directory -Path ".\artifacts\NestorPro\Drivers" -Force
Copy-Item -LiteralPath ".\src\NestorPro\native\install-interception.exe" -Destination ".\artifacts\NestorPro\Drivers\install-interception.exe"
```

O script de preparação não inclui instalador ViGEmBus. Obtenha-o no projeto oficial e siga os termos correspondentes.

## Testes adicionais da interface

O aplicativo possui --self-test, --mouse-stick-test e --direct-output-test. O teste de saída cria e desconecta um controle virtual temporário; execute-o fora da partida. Não substitui medição de latência ou um teste físico no jogo.

A validação descrita em VALIDACAO.md continua tendo os mesmos limites.
