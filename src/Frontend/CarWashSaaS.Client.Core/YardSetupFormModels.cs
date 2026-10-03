using System.ComponentModel.DataAnnotations;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class YardCapacityFormModel
{
    [Required(ErrorMessage = "A quantidade de boxes é obrigatória.")]
    [Range(1, 100, ErrorMessage = "A capacidade deve ser entre 1 e 100 boxes/vagas simultâneas.")]
    public int TotalBoxes { get; set; } = 4;

    [MaxLength(200, ErrorMessage = "A descrição não pode ultrapassar 200 caracteres.")]
    public string Description { get; set; } = "Pátio principal";

    public CreateYardCapacityRequest ToCreateRequest() => new(TotalBoxes, Description);
    public UpdateYardCapacityRequest ToUpdateRequest() => new(TotalBoxes, Description);

    public void LoadFrom(YardCapacityDto dto)
    {
        TotalBoxes = dto.TotalBoxes;
        Description = dto.Description;
    }
}

public sealed class TeamMemberFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O nome completo do colaborador é obrigatório.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 200 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A função/cargo é obrigatória.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O cargo deve ter entre 2 e 80 caracteres.")]
    public string Role { get; set; } = "Lavador";

    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [MaxLength(200, ErrorMessage = "O e-mail não pode ultrapassar 200 caracteres.")]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public CreateTeamMemberRequest ToCreateRequest() => new(FullName.Trim(), Role.Trim(), string.IsNullOrWhiteSpace(Email) ? null : Email.Trim());
    public UpdateTeamMemberRequest ToUpdateRequest() => new(FullName.Trim(), Role.Trim(), string.IsNullOrWhiteSpace(Email) ? null : Email.Trim());

    public void LoadFrom(TeamMemberDto dto)
    {
        Id = dto.Id;
        FullName = dto.FullName;
        Role = dto.Role;
        Email = dto.Email;
        IsActive = dto.IsActive;
    }

    public void Reset()
    {
        Id = null;
        FullName = string.Empty;
        Role = "Lavador";
        Email = null;
        IsActive = true;
    }
}

public sealed class CommissionRuleFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O nome do serviço é obrigatório.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "O serviço deve ter entre 2 e 200 caracteres.")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O cargo do colaborador é obrigatório.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O cargo deve ter entre 2 e 80 caracteres.")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O percentual de comissão é obrigatório.")]
    [Range(0.0, 100.0, ErrorMessage = "O percentual deve estar entre 0% e 100%.")]
    public decimal Percentage { get; set; } = 10.0m;

    public CreateCommissionRuleRequest ToCreateRequest() => new(ServiceName.Trim(), RoleName.Trim(), Percentage);
    public UpdateCommissionRuleRequest ToUpdateRequest() => new(Percentage);

    public void LoadFrom(CommissionRuleDto dto)
    {
        Id = dto.Id;
        ServiceName = dto.ServiceName;
        RoleName = dto.RoleName;
        Percentage = dto.Percentage;
    }

    public void Reset()
    {
        Id = null;
        ServiceName = string.Empty;
        RoleName = string.Empty;
        Percentage = 10.0m;
    }
}
