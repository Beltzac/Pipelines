# Visão Geral do Projeto

Este projeto inclui vários componentes para gerenciar informações de build, gerenciamento de chaves do Consul, requisições de execução, comparação de schemas Oracle e outras funcionalidades. Abaixo está um resumo da funcionalidade de cada página:

## Funcionalidades

1.  **Informações de Build**: Visualize informações sobre os builds de diferentes repositórios, com opções para filtrar, clonar repositórios, abrir projetos no VS Code, visualizar logs de erro, abrir commits no browser, abrir pipelines no browser, abrir projetos no SonarCloud, deletar e refetchar repositórios.

2.  **Commits**: Veja uma lista de commits agrupados por data, com a possibilidade de sincronizar commits, baixá-los em Excel e copiar IDs de cartões Jira.

3.  **Gerenciador de Configurações**: Gerencie as configurações do aplicativo, incluindo a importação/exportação de configurações, configuração de usuário, organização, github, regras para ignorar repositórios, configuração de ambientes Consul e Oracle e a configuração de trabalhos de backup. Permite também testar conexões com o Oracle e executar os jobs de backup manualmente.

4.  **Gerenciamento de Chaves do Consul**: Gerencie pares chave-valor do Consul, permitindo carregar chaves, salvá-las em uma pasta, abrir a pasta no Visual Studio Code, pesquisar chaves e valores, visualizar valores recursivamente e salvar valores no Consul.

5.  **Consulta de Requisições (ESB)**: Execute consultas relacionadas a requisições de execução com vários filtros, como ambiente, intervalo de datas, URL/Nome do fluxo, método HTTP, números de container, usuário, ID de execução, range de status HTTP e status de resposta. Visualize os resultados em uma tabela com paginação, veja detalhes das requisições, copie os dados para a área de transferência e exporte a query SQL.

6.  **Consulta LTDB/LTVC (SGG)**: Consulte registros LTDB/LTVC com filtros para ambiente, intervalo de datas, placa, ID da requisição, números de container, ID de agendamento, tipo de movimentação e status. Exiba os resultados em uma tabela com paginação, visualize os tempos de resposta em um gráfico, veja detalhes dos registros e exporte a query SQL.

7.  **Comparação de Schemas Oracle**: Compare schemas Oracle, permitindo identificar diferenças entre ambientes e exportar o DDL de views.

8.  **Gerenciamento de Inicialização no Windows**: Permite que a aplicação seja configurada para iniciar com o Windows.

9.  **Minimização para a Bandeja do Sistema**: A aplicação pode ser minimizada para a bandeja do sistema, permitindo que os usuários continuem a usar o computador sem fechar completamente a aplicação. Os usuários podem interagir com o ícone da bandeja para restaurar a aplicação ou acessar opções adicionais.

10. **Atualização Automática**: O aplicativo verifica automaticamente por atualizações, garantindo que você esteja sempre usando a versão mais recente.
