// T08b - verificacao 3: exercitar a interface do Swagger por cliques reais.
// Uso: node swagger-ui-authorize.mjs <dir-das-capturas>
// Sai com codigo 0 so se todas as afirmacoes passarem.
import { chromium } from 'file:///C:/Users/2273129/Documents/Projects/TemplateGenerator/src/web/node_modules/playwright/index.mjs';
import path from 'node:path';
import fs from 'node:fs';

const BASE = 'http://localhost:5100';
const OUT = process.argv[2];
if (!OUT) { console.error('uso: node swagger-ui-authorize.mjs <dir-das-capturas>'); process.exit(2); }
fs.mkdirSync(OUT, { recursive: true });

const EMAIL = `qa.t08b.${Date.now()}@exemplo.com`;
const PASSWORD = 'Senha!123';
const ITEM_TITLE = 'Item criado pelo Swagger UI';

let failures = 0;
let token = null;
const log = (...a) => console.log(...a);
function check(step, cond, detail) {
  log(`${cond ? 'PASS' : 'FAIL'}  ${step}${detail ? ' :: ' + detail : ''}`);
  if (!cond) failures++;
  return cond;
}
const shot = (name) => path.join(OUT, name);

// Bloco de operacao pelo metodo e pelo caminho exato (data-path), sem depender de operationId.
function op(page, method, p) {
  return page.locator('.opblock')
    .filter({ has: page.locator('.opblock-summary-method', { hasText: new RegExp(`^${method}$`) }) })
    .filter({ has: page.locator(`.opblock-summary-path[data-path="${p}"]`) });
}

async function expand(block) {
  if (!(await block.evaluate((el) => el.classList.contains('is-open')))) {
    await block.locator('.opblock-summary-control').first().click();
  }
  await block.locator('.opblock-body').waitFor({ state: 'visible' });
}

async function tryOutAndExecute(block, body) {
  await expand(block);
  const tryBtn = block.locator('button.try-out__btn');
  if ((await tryBtn.innerText()).trim().toLowerCase().startsWith('try it out')) await tryBtn.click();
  if (body !== undefined) {
    const ta = block.locator('textarea.body-param__text');
    await ta.waitFor({ state: 'visible' });
    await ta.fill(body);
  }
  // Resposta anterior na tela confunde a leitura: limpa com o botao "Clear" da propria interface.
  const clearBtn = block.locator('button.btn-clear');
  if (await clearBtn.count()) await clearBtn.first().click();
  await block.locator('.live-responses-table').waitFor({ state: 'detached', timeout: 5000 }).catch(() => {});
  const page = block.page();
  const method = (await block.locator('.opblock-summary-method').first().innerText()).trim();
  const p = await block.locator('.opblock-summary-path').first().getAttribute('data-path');
  const netWait = page.waitForResponse((r) => r.request().method() === method && new URL(r.url()).pathname === p, { timeout: 20000 });
  await block.locator('button.execute').click();
  const net = await netWait;
  const status = block.locator('.live-responses-table .response .response-col_status').first();
  await status.waitFor({ state: 'visible', timeout: 20000 });
  const raw = (await status.innerText()).trim();
  const code = (raw.match(/\d{3}/) || [raw])[0];
  const statusLabel = raw.replace(/\s+/g, ' ');
  const respBody = (await block.locator('.live-responses-table .response .response-col_description').first().innerText()).trim();
  const curl = (await block.locator('.curl-command').first().innerText().catch(() => '')).trim();
  const reqUrl = (await block.locator('.request-url').first().innerText().catch(() => '')).trim();
  const netCode = String(net.status());
  const sentAuth = (await net.request().allHeaders())['authorization'] || '';
  log(`      ${method} ${p}: status na interface="${statusLabel}", status na rede=${netCode}, Authorization enviado=${sentAuth ? sentAuth.split(' ')[0] + ' <token ' + (sentAuth.length - sentAuth.indexOf(' ') - 1) + ' caracteres>' : '(nenhum)'}`);
  if (netCode !== code) { failures++; log(`FAIL  ${method} ${p}: a interface mostra ${code} mas a rede respondeu ${netCode}`); }
  return { code, respBody, curl, reqUrl, netCode, sentAuth };
}

// Troca o token pelo prefixo + marcador em todo texto renderizado; so na tela, depois das afirmacoes.
async function maskTokensInDom(page, secrets) {
  await page.evaluate((list) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    for (const n of nodes) {
      let t = n.nodeValue;
      for (const s of list) if (s && t.includes(s)) t = t.split(s).join(`${s.slice(0, 6)}...[mascarado, ${s.length} caracteres]`);
      if (t !== n.nodeValue) n.nodeValue = t;
    }
  }, secrets);
  const leaked = await page.evaluate((list) => {
    const txt = document.body.innerText;
    const vals = [...document.querySelectorAll('input,textarea')].map((e) => e.value).join('\n');
    return list.some((s) => s && (txt.includes(s) || vals.includes(s)));
  }, secrets);
  return !leaked;
}

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1400, height: 1000 } });
const page = await context.newPage();
const secrets = [];

try {
  // --- b. abrir a interface
  const resp = await page.goto(`${BASE}/swagger`, { waitUntil: 'networkidle' });
  check('b. GET /swagger abre a interface', resp && resp.status() === 200, `http=${resp && resp.status()} url=${page.url()}`);
  await page.locator('.opblock').first().waitFor({ state: 'visible', timeout: 20000 });

  const authorizeBtn = page.locator('.auth-wrapper button.authorize');
  check('b. botao Authorize visivel', await authorizeBtn.isVisible(), `texto="${(await authorizeBtn.innerText().catch(() => '')).trim()}"`);

  const lockFor = (m, p) => op(page, m, p).locator('.opblock-summary .authorization__btn');
  for (const [m, p] of [['GET', '/items'], ['POST', '/items'], ['GET', '/items/{id}'], ['PUT', '/items/{id}'], ['DELETE', '/items/{id}'], ['GET', '/auth/manage/info'], ['POST', '/auth/manage/info'], ['POST', '/auth/manage/2fa']]) {
    const l = lockFor(m, p);
    const n = await l.count();
    check(`b. cadeado em ${m} ${p}`, n === 1 && (await l.isVisible()), `aria-label="${n ? await l.getAttribute('aria-label') : '-'}"`);
  }
  for (const [m, p] of [['GET', '/health'], ['POST', '/auth/register'], ['POST', '/auth/login'], ['POST', '/auth/refresh']]) {
    const blk = op(page, m, p);
    check(`b. ${m} ${p} existe e NAO tem cadeado`, (await blk.count()) === 1 && (await lockFor(m, p).count()) === 0);
  }
  await page.screenshot({ path: shot('01-swagger-ui-authorize-e-cadeados.png'), fullPage: true });
  log('CAPTURA 01-swagger-ui-authorize-e-cadeados.png');

  // --- a. cadastro e login pela interface, sem token
  const reg = await tryOutAndExecute(op(page, 'POST', '/auth/register'), JSON.stringify({ email: EMAIL, password: PASSWORD }, null, 2));
  check('a. POST /auth/register pela interface (sem token) responde 200', reg.code === '200', `status=${reg.code} email=${EMAIL}`);
  check('a. curl do register nao manda Authorization', !/authorization/i.test(reg.curl));

  const loginBlock = op(page, 'POST', '/auth/login');
  const login = await tryOutAndExecute(loginBlock, JSON.stringify({ email: EMAIL, password: PASSWORD }, null, 2));
  check('a. POST /auth/login pela interface (sem token) responde 200', login.code === '200', `status=${login.code}`);
  const mAccess = login.respBody.match(/"accessToken"\s*:\s*"([^"]+)"/);
  const mRefresh = login.respBody.match(/"refreshToken"\s*:\s*"([^"]+)"/);
  const mType = login.respBody.match(/"tokenType"\s*:\s*"([^"]+)"/);
  token = mAccess ? mAccess[1] : null;
  if (token) secrets.push(token);
  if (mRefresh) secrets.push(mRefresh[1]);
  check('a. resposta do login traz accessToken', !!token, token ? `tokenType=${mType && mType[1]} accessToken.length=${token.length} prefixo=${token.slice(0, 5)}` : `corpo=${login.respBody.slice(0, 200)}`);
  if (!token) throw new Error('sem accessToken: nao da para seguir');
  const okMaskLogin = await maskTokensInDom(page, secrets);
  check('a. tokens mascarados na tela antes da captura do login', okMaskLogin);
  await loginBlock.screenshot({ path: shot('00-login-pela-interface-200.png') });
  log('CAPTURA 00-login-pela-interface-200.png');
  // recolhe register e login para a tela ficar limpa
  for (const [m, p] of [['POST', '/auth/register'], ['POST', '/auth/login']]) await op(page, m, p).locator('.opblock-summary-control').first().click();

  // --- c. antes de autorizar: GET /items -> 401
  const getItems = op(page, 'GET', '/items');
  const r401 = await tryOutAndExecute(getItems);
  check('c. GET /items antes de Authorize responde 401', r401.code === '401', `status=${r401.code}`);
  check('c. curl antes de Authorize NAO tem Authorization', !/authorization/i.test(r401.curl), r401.curl.replace(/\s+/g, ' '));
  check('c. a requisicao de rede nao levou Authorization', r401.sentAuth === '');
  await getItems.screenshot({ path: shot('02-get-items-sem-authorize-401.png') });
  log('CAPTURA 02-get-items-sem-authorize-401.png');

  // --- d. Authorize: so o token, sem "Bearer "
  await authorizeBtn.click();
  const modal = page.locator('.modal-ux');
  await modal.waitFor({ state: 'visible' });
  const container = modal.locator('.auth-container').first();
  const header = (await container.locator('h4').first().innerText()).trim();
  check('d. dialogo mostra o esquema Bearer (http, Bearer)', /Bearer/.test(header) && /http/i.test(header), `cabecalho="${header.replace(/\s+/g, ' ')}"`);
  const input = container.locator('input').first();
  await input.fill(token);
  await page.screenshot({ path: shot('03a-dialogo-authorize-token-colado.png'), mask: [input] });
  log('CAPTURA 03a-dialogo-authorize-token-colado.png (campo do token coberto pela mascara do Playwright)');
  await modal.locator('.auth-btn-wrapper button.authorize').click();
  const authorized = modal.locator('.auth-container', { hasText: 'Authorized' });
  await authorized.first().waitFor({ state: 'visible', timeout: 10000 });
  check('d. dialogo mostra Authorized depois de confirmar', await authorized.first().isVisible());
  const okMaskDlg = await maskTokensInDom(page, secrets);
  check('d. token nao aparece no dialogo autorizado', okMaskDlg);
  await page.screenshot({ path: shot('03b-dialogo-authorize-autorizado.png') });
  log('CAPTURA 03b-dialogo-authorize-autorizado.png');
  await modal.locator('.auth-btn-wrapper button.btn-done, button.close-modal').first().click();
  await modal.waitFor({ state: 'hidden' });
  check('d. dialogo fechado', !(await modal.isVisible()));
  const lockAfter = await lockFor('GET', '/items').getAttribute('aria-label');
  check('d. cadeado de GET /items passa a "locked"', /authorization button locked/.test(lockAfter || ''), `aria-label="${lockAfter}"`);

  // --- e. GET /items -> 200, curl com Authorization: Bearer <token>
  const r200 = await tryOutAndExecute(getItems);
  check('e. GET /items depois de Authorize responde 200', r200.code === '200', `status=${r200.code} corpo=${r200.respBody.replace(/\s+/g, ' ').slice(0, 120)}`);
  const curlHasBearer = r200.curl.includes(`Authorization: Bearer ${token}`);
  check('e. curl mostrado contem "Authorization: Bearer <o token do login>"', curlHasBearer);
  check('e. curl NAO duplica o prefixo ("Bearer Bearer")', !/Bearer\s+Bearer/i.test(r200.curl));
  check('e. a requisicao de rede levou exatamente "Bearer <token>"', r200.sentAuth === `Bearer ${token}`);
  const okMask200 = await maskTokensInDom(page, secrets);
  check('e. token mascarado na tela antes da captura', okMask200);
  await getItems.screenshot({ path: shot('04-get-items-com-authorize-200-curl-bearer.png') });
  log('CAPTURA 04-get-items-com-authorize-200-curl-bearer.png');
  await getItems.locator('.opblock-summary-control').first().click();

  // --- f. bonus: POST /items -> 201 e GET /auth/manage/info -> 200
  const postItems = op(page, 'POST', '/items');
  const r201 = await tryOutAndExecute(postItems, JSON.stringify({ title: ITEM_TITLE }, null, 2));
  check('f. POST /items depois de Authorize responde 201', r201.code === '201', `status=${r201.code} corpo=${r201.respBody.replace(/\s+/g, ' ').slice(0, 160)}`);
  check('f. curl do POST /items contem Authorization: Bearer <token>', r201.curl.includes(`Authorization: Bearer ${token}`));
  await maskTokensInDom(page, secrets);
  await postItems.screenshot({ path: shot('05-post-items-com-authorize-201.png') });
  log('CAPTURA 05-post-items-com-authorize-201.png');
  await postItems.locator('.opblock-summary-control').first().click();

  const r200b = await tryOutAndExecute(getItems);
  check('f. GET /items de novo responde 200 e lista o item criado', r200b.code === '200' && r200b.respBody.includes(ITEM_TITLE), `status=${r200b.code}`);
  await getItems.locator('.opblock-summary-control').first().click();

  const manage = op(page, 'GET', '/auth/manage/info');
  const rInfo = await tryOutAndExecute(manage);
  check('f. GET /auth/manage/info depois de Authorize responde 200 com o e-mail', rInfo.code === '200' && rInfo.respBody.includes(EMAIL), `status=${rInfo.code} corpo=${rInfo.respBody.replace(/\s+/g, ' ').slice(0, 160)}`);
  check('f. curl do manage/info contem Authorization: Bearer <token>', rInfo.curl.includes(`Authorization: Bearer ${token}`));
  await maskTokensInDom(page, secrets);
  await manage.screenshot({ path: shot('06-get-auth-manage-info-com-authorize-200.png') });
  log('CAPTURA 06-get-auth-manage-info-com-authorize-200.png');
} catch (e) {
  failures++;
  log(`FAIL  excecao: ${String(e && e.message ? e.message : e).split('\n')[0]}`);
  try { await maskTokensInDom(page, secrets); await page.screenshot({ path: shot('99-falha.png'), fullPage: true }); log('CAPTURA 99-falha.png'); } catch {}
} finally {
  await browser.close();
}

log(`RESULTADO: ${failures === 0 ? 'TODAS AS AFIRMACOES PASSARAM' : failures + ' FALHA(S)'}`);
process.exit(failures === 0 ? 0 : 1);
