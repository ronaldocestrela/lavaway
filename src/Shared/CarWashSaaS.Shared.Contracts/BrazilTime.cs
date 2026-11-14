namespace CarWashSaaS.Shared.Contracts;

/// <summary>
/// Centraliza a resolução de data e hora oficial do Brasil (Horário de Brasília - America/Sao_Paulo / UTC-03:00),
/// garantindo comportamento idêntico independente do fuso horário da VPS, container Docker ou navegador.
/// </summary>
public static class BrazilTime
{
    public const string IanaTimeZoneId = "America/Sao_Paulo";
    public const string WindowsTimeZoneId = "E. South America Standard Time";
    public static readonly TimeSpan StandardOffset = TimeSpan.FromHours(-3);

    public static TimeZoneInfo TimeZone { get; } = ResolveBrazilTimeZone();

    /// <summary>
    /// Data e hora atual no fuso horário de Brasília (UTC-03:00).
    /// </summary>
    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone);

    /// <summary>
    /// Data atual (DateOnly) no fuso horário de Brasília.
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>
    /// Hora atual (TimeOnly) no fuso horário de Brasília.
    /// </summary>
    public static TimeOnly CurrentTime => TimeOnly.FromDateTime(Now.DateTime);

    /// <summary>
    /// Converte um <see cref="DateTimeOffset"/> (ex.: UTC) para o fuso horário de Brasília (UTC-03:00).
    /// </summary>
    public static DateTimeOffset ToBrazilTime(this DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TimeZone);

    /// <summary>
    /// Converte um <see cref="DateTime"/> para o fuso horário de Brasília (assumindo UTC quando Unspecified).
    /// </summary>
    public static DateTime ToBrazilTime(this DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }

    /// <summary>
    /// Extrai a data (<see cref="DateOnly"/>) correspondente no fuso horário de Brasília a partir de um <see cref="DateTimeOffset"/>.
    /// </summary>
    public static DateOnly ToBrazilDateOnly(this DateTimeOffset value) =>
        DateOnly.FromDateTime(value.ToBrazilTime().DateTime);

    /// <summary>
    /// Retorna o início do dia comercial no Brasil (00:00:00 -03:00) convertido para UTC (03:00:00Z).
    /// </summary>
    public static DateTimeOffset StartOfDayUtc(this DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), StandardOffset).ToUniversalTime();

    /// <summary>
    /// Retorna o fim do dia comercial no Brasil (23:59:59.9999999 -03:00) convertido para UTC (02:59:59.9999999Z do dia seguinte).
    /// </summary>
    public static DateTimeOffset EndOfDayUtc(this DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), StandardOffset).ToUniversalTime();

    private static TimeZoneInfo ResolveBrazilTimeZone()
    {
        // Como o Brasil aboliu o horário de verão desde 2019 (Decreto nº 9.772/2019) e opera fixo em UTC-03:00 (Brasília),
        // criamos um TimeZoneInfo com offset fixo de -03:00 caso o sistema operacional ou runtime WebAssembly
        // tenha regras históricas desatualizadas ou não possua base tzdata instalada.
        return TimeZoneInfo.CreateCustomTimeZone(
            id: IanaTimeZoneId,
            baseUtcOffset: StandardOffset,
            displayName: "(UTC-03:00) Horário Padrão de Brasília",
            standardDisplayName: "Horário Padrão de Brasília");
    }
}

