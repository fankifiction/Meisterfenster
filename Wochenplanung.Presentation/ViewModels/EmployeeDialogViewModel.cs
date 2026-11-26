using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.ViewModels;

public partial class EmployeeDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _employeeNumber = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _role = string.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isActive = true;

    public Employee? Employee { get; set; }

    public EmployeeDialogViewModel(Employee? employee = null)
    {
        if (employee != null)
        {
            Employee = employee;
            EmployeeNumber = employee.EmployeeNumber ?? string.Empty;
            FirstName = employee.FirstName;
            LastName = employee.LastName;
            Role = employee.Role ?? string.Empty;
            IsActive = employee.IsActive;
            IsEditMode = true;
        }
    }

    public Employee GetEmployee()
    {
        if (Employee != null)
        {
            Employee.EmployeeNumber = string.IsNullOrWhiteSpace(EmployeeNumber) ? null : EmployeeNumber.Trim();
            Employee.FirstName = FirstName;
            Employee.LastName = LastName;
            Employee.Role = string.IsNullOrWhiteSpace(Role) ? null : Role.Trim();
            Employee.IsActive = IsActive;
            return Employee;
        }

        return new Employee
        {
            EmployeeNumber = string.IsNullOrWhiteSpace(EmployeeNumber) ? null : EmployeeNumber.Trim(),
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Role = string.IsNullOrWhiteSpace(Role) ? null : Role.Trim(),
            IsActive = IsActive
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // Speichern wird vom Dialog gehandhabt
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);
    }
}
