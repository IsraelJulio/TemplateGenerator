import { defineConfig, devices } from '@playwright/test';

/**
 * O fluxo ponta a ponta de `docs/quality/test-strategy.md`, seção "Frontend":
 * preencher a tela, baixar o ZIP e **inspecionar o conteúdo baixado**.
 *
 * A razão de ele existir separado do Vitest está no critério de aceite 11 de
 * T03. Até T02 o contrato respondia `501` a uma configuração válida, então o
 * estado "download concluído" só podia ser demonstrado com dublê de rede —
 * `HttpTestingController` e um `Blob` fabricado no próprio teste, que continua
 * em `configurator.spec.ts` e continua valendo para os oito estados da tela.
 * Com o `200 application/zip` real, um dublê deixou de bastar: quem afirma que
 * o download funciona precisa falar com o gerador de verdade.
 *
 * **As duas pontas sobem aqui**, e é isso que separa este teste de um dublê
 * melhor disfarçado: a API .NET no perfil `http` e o `ng serve` com o proxy de
 * `proxy.conf.json` apontado para ela. Nenhuma rota é interceptada, nenhum
 * corpo é fabricado; o ZIP que o navegador salva é o que o motor montou.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',

  /* Um envio de formulário que espera `dotnet run` acordar é lento por fora,
     não por dentro: o retry esconderia justamente a falha intermitente que
     interessa. */
  retries: 0,
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],

  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },

  /* RNF-10 e o critério do papel `frontend`: desktop **e** celular. O mesmo
     fluxo roda nos dois, porque um layout que empilha e esconde o botão de
     enviar é um defeito de celular que nenhum teste de desktop pega. */
  projects: [
    {
      name: 'desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    {
      name: 'celular',
      use: { ...devices['Pixel 7'] },
    },
  ],

  webServer: [
    {
      /* O mesmo perfil que `proxy.conf.json` tem por alvo. Se a porta do perfil
         mudar, os dois mudam junto. */
      command: 'dotnet run --project ../TemplateGenerator.Api --launch-profile http',
      url: 'http://localhost:5080/api/health',
      timeout: 180_000,
      reuseExistingServer: true,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'npm start -- --port 4200',
      url: 'http://localhost:4200',
      timeout: 180_000,
      reuseExistingServer: true,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
