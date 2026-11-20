# Validação — evidências e limites

Base documentada: NestorPro DIRETO 0.3.9.4.

## Evidências disponíveis

- Testes locais de efeitos, gatilhos, limites de valores, persistência e independência dos eixos.
- Verificação automatizada de 20.000 estados sem aplicação de efeitos quando os gatilhos/contextos exigidos não estão ativos. Não representa 20.000 partidas ou computadores.
- Testes sintéticos com seis cadências de entrada de mouse: 1, 2, 4, 8, 10 e 16 ms.
- Testes de roteamento para confirmação de compras, navegação, descarte, reset e comandos de menu.
- O painel abriu e enviou relatórios virtuais em um segundo ambiente Windows após preparar os drivers e reiniciar.

Os intervalos sintéticos não são medidas de latência ponta a ponta. Contadores de entrada e saída não medem o tempo entre mover o mouse e ver a imagem no monitor.

## O que permanece pendente

- Ensaio completo de jogo com mouse conectado fisicamente ao segundo computador.
- Matriz reproduzível de versões do Windows, dispositivos e configurações do jogo.
- Pacote de instalação e desinstalação validado em uma máquina limpa.
- Revisão de licenças e redistribuição das dependências antes da distribuição comercial.
- Benchmark de resposta com método e limites de erro documentados.

## Roteiro para cada novo ambiente

1. Registrar versão do aplicativo, Windows, mouse, DPI, frequência e versão do jogo.
2. Abrir o painel e verificar erros na aba Sistema.
3. Confirmar a presença e a resposta do controle virtual no sistema.
4. Mover o mouse lentamente e rapidamente em X e Y; parar e verificar retorno ao centro.
5. Comparar movimento com e sem ADS usando valores conhecidos.
6. Testar HOME para liberar e retomar o cursor.
7. Na loja, selecionar, navegar entre colunas e confirmar compras. Verificar se o modo de disparo da arma foi preservado.
8. No inventário, testar dinheiro, placas, munições e armas separadamente.
9. Reabrir o aplicativo e conferir a persistência dos ajustes.
10. Documentar qualquer falha com passos e evidência sem dados pessoais.

Registre o resultado como passou, falhou ou não testado. Não substitua não testado por passou.
