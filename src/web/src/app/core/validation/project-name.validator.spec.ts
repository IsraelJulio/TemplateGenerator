import { FormControl } from '@angular/forms';
import { describeProjectNameProblem, projectNameValidator } from './project-name.validator';

describe('describeProjectNameProblem', () => {
  it('aceita um nome com segmentos válidos', () => {
    expect(describeProjectNameProblem('Acme.Billing.Api')).toBeNull();
    expect(describeProjectNameProblem('_Interno')).toBeNull();
  });

  it('recusa nome vazio', () => {
    expect(describeProjectNameProblem('')).toBe('Informe o nome do projeto.');
  });

  it('recusa mais de 100 caracteres', () => {
    expect(describeProjectNameProblem('A'.repeat(101))).toContain('100 caracteres');
  });

  it('recusa segmento que começa com dígito', () => {
    expect(describeProjectNameProblem('1Acme')).toContain('começa com letra');
  });

  it('recusa espaço e hífen', () => {
    expect(describeProjectNameProblem('Acme Billing')).toContain('Use letras');
    expect(describeProjectNameProblem('Acme-Billing')).toContain('Use letras');
  });

  it('recusa barra, contrabarra e ponto duplo', () => {
    expect(describeProjectNameProblem('Acme/Api')).toBe('O nome não pode conter barras.');
    expect(describeProjectNameProblem('Acme\\Api')).toBe('O nome não pode conter barras.');
    expect(describeProjectNameProblem('Acme..Api')).toBe(
      'O nome não pode conter dois pontos seguidos.',
    );
  });

  it('recusa palavra reservada do C# em qualquer segmento', () => {
    expect(describeProjectNameProblem('Acme.class.Api')).toContain('palavra reservada do C#');
  });

  it('recusa nome reservado do Windows em qualquer segmento', () => {
    expect(describeProjectNameProblem('CON')).toContain('reservado do Windows');
    expect(describeProjectNameProblem('Acme.com1.Api')).toContain('reservado do Windows');
  });

  it('recusa caractere de controle', () => {
    expect(describeProjectNameProblem(`Acme${String.fromCharCode(7)}Api`)).toBe(
      'O nome não pode conter caracteres de controle.',
    );
  });
});

describe('projectNameValidator', () => {
  it('devolve a mensagem sob a chave projectName', () => {
    const control = new FormControl('Acme Billing');

    expect(projectNameValidator(control)).toEqual({
      projectName: { message: expect.stringContaining('Use letras') },
    });
  });

  it('ignora espaços nas pontas', () => {
    expect(projectNameValidator(new FormControl('  Acme.Api  '))).toBeNull();
  });
});
