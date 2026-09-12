using TemplateGenerator.Generation.Validation;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Validation;

/// <summary>
/// A regra de <c>projectName</c> de docs/product/option-matrix.md, item a item.
/// </summary>
/// <remarks>
/// O nome vira namespace, assembly, diretório dentro do ZIP e nome do arquivo baixado. Por isso
/// os casos hostis — <c>..</c>, barra, <c>CON</c> — não são curiosidade: são o vetor pelo qual um
/// nome escaparia da raiz virtual do pacote ou produziria um ZIP impossível de extrair.
/// </remarks>
public sealed class ProjectNameValidatorTests
{
    [Theory]
    [InlineData("Acme.Billing.Api")]
    [InlineData("A")]
    [InlineData("_interno.Api")]
    [InlineData("Acme2.Api10")]
    [InlineData("Class")]          // palavra reservada só em minúscula; `Class` é identificador
    [InlineData("Con1")]           // CON é reservado; CON1 não é
    [InlineData("Acme_Billing")]
    [InlineData("Cotacao.Api")]    // sem acento: aceito
    public void Aceita_nome_valido(string projectName)
    {
        IReadOnlyList<string> errors = ProjectNameValidator.Validate(projectName);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(" Acme.Api ")]
    [InlineData("\tAcme.Api")]
    [InlineData("Acme.Api\r\n")]
    public void Apara_espacos_nas_pontas_antes_de_validar(string projectName)
    {
        // O frontend apara antes de enviar. Se o servidor não aparasse, recusaria de quem chama
        // a API direto exatamente o que aceita de quem usa a tela
        // (docs/product/option-matrix.md, "Espaço em branco").
        Assert.Empty(ProjectNameValidator.Validate(projectName));

        Assert.Equal("Acme.Api", ProjectNameValidator.Normalize(projectName));
    }

    [Fact]
    public void Espaco_no_meio_continua_recusado()
    {
        // Aparar é só nas pontas: 'Acme Billing' não vira 'AcmeBilling'.
        Assert.NotEmpty(ProjectNameValidator.Validate(" Acme Billing "));
    }

    [Theory]
    [InlineData("Cotação.Api")]
    [InlineData("Ação")]
    [InlineData("Acme.Configuração.Api")]
    [InlineData("Grüße")]
    [InlineData("Москва")]
    public void Recusa_letra_fora_do_ASCII(string projectName)
    {
        // O C# aceitaria todos estes como identificador. A regra do produto não
        // (docs/product/option-matrix.md, "Por que ASCII e não Unicode").
        IReadOnlyList<string> errors = ProjectNameValidator.Validate(projectName);

        Assert.NotEmpty(errors);

        // A mensagem precisa dizer o que fazer, não só recusar.
        Assert.Contains(
            errors,
            message => message.Contains("letras sem acento", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "Informe")]
    [InlineData("", "Informe")]
    [InlineData("   ", "Informe")]
    [InlineData("..", "'..'")]
    [InlineData("Acme..Api", "'..'")]
    [InlineData("../etc/passwd", "'..'")]
    [InlineData("Acme/Billing", "'/'")]
    [InlineData("Acme\\Billing", "'/'")]
    [InlineData("Acme\u0001Api", "controle")]
    [InlineData("Acme.", "precisa ser preenchido")]
    [InlineData(".Acme", "precisa ser preenchido")]
    [InlineData("1Acme", "identificador C# válido")]
    [InlineData("Acme Billing", "identificador C# válido")]
    [InlineData("Acme-Billing", "identificador C# válido")]
    [InlineData("Acme.Billing!", "identificador C# válido")]
    [InlineData("class", "palavra reservada do C#")]
    [InlineData("namespace", "palavra reservada do C#")]
    [InlineData("Acme.static.Api", "palavra reservada do C#")]
    [InlineData("CON", "reservado do Windows")]
    [InlineData("con", "reservado do Windows")]
    [InlineData("NUL", "reservado do Windows")]
    [InlineData("COM1", "reservado do Windows")]
    [InlineData("LPT9", "reservado do Windows")]
    [InlineData("Acme.AUX.Api", "reservado do Windows")]
    public void Recusa_nome_invalido(string? projectName, string expectedFragment)
    {
        IReadOnlyList<string> errors = ProjectNameValidator.Validate(projectName);

        Assert.NotEmpty(errors);

        Assert.Contains(
            errors,
            message => message.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void Aceita_nome_no_limite_de_cem_caracteres()
    {
        string projectName = new('A', ProjectNameValidator.MaximumLength);

        Assert.Empty(ProjectNameValidator.Validate(projectName));
    }

    [Fact]
    public void Recusa_nome_acima_do_limite()
    {
        string projectName = new('A', ProjectNameValidator.MaximumLength + 1);

        IReadOnlyList<string> errors = ProjectNameValidator.Validate(projectName);

        Assert.Contains(errors, message => message.Contains("no máximo 100", StringComparison.Ordinal));
    }

    [Fact]
    public void Mensagem_nomeia_o_trecho_culpado()
    {
        IReadOnlyList<string> errors = ProjectNameValidator.Validate("Acme.1Billing.Api");

        // Quem lê precisa saber qual dos três trechos está errado, não só que "o nome" está.
        Assert.Contains(errors, message => message.Contains("'1Billing'", StringComparison.Ordinal));
    }

    [Fact]
    public void Estrutura_quebrada_nao_produz_erro_repetido_por_trecho()
    {
        // 'a/b/c' já é um erro completo: acusar três trechos inválidos por cima só afoga o
        // diagnóstico que a pessoa precisa ler.
        IReadOnlyList<string> errors = ProjectNameValidator.Validate("a/b/c");

        Assert.Single(errors);
    }
}
