namespace Common.Models
{
    public class WorklogTimePreset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int TimeInMinutes { get; set; }
        public string IconClass { get; set; }
        public string ColorClass { get; set; }
    }

    public static class WorklogPresets
    {
        public static List<WorklogTimePreset> DefaultPresets => new()
        {
            new WorklogTimePreset
            {
                Name = "15min",
                Description = "Revisão rápida",
                TimeInMinutes = 15,
                IconClass = "fa-bolt",
                ColorClass = "btn-info"
            },
            new WorklogTimePreset
            {
                Name = "30min",
                Description = "Tarefa pequena",
                TimeInMinutes = 30,
                IconClass = "fa-clock",
                ColorClass = "btn-primary"
            },
            new WorklogTimePreset
            {
                Name = "1h",
                Description = "Desenvolvimento padrão",
                TimeInMinutes = 60,
                IconClass = "fa-code",
                ColorClass = "btn-primary"
            },
            new WorklogTimePreset
            {
                Name = "1h30",
                Description = "Feature média",
                TimeInMinutes = 90,
                IconClass = "fa-laptop-code",
                ColorClass = "btn-primary"
            },
            new WorklogTimePreset
            {
                Name = "2h",
                Description = "GM / Acompanhamento",
                TimeInMinutes = 120,
                IconClass = "fa-tools",
                ColorClass = "btn-success"
            },
            new WorklogTimePreset
            {
                Name = "3h",
                Description = "Desenvolvimento complexo",
                TimeInMinutes = 180,
                IconClass = "fa-project-diagram",
                ColorClass = "btn-warning"
            },
            new WorklogTimePreset
            {
                Name = "4h",
                Description = "Half day",
                TimeInMinutes = 240,
                IconClass = "fa-calendar-check",
                ColorClass = "btn-warning"
            },
            new WorklogTimePreset
            {
                Name = "8h",
                Description = "Dia completo",
                TimeInMinutes = 480,
                IconClass = "fa-calendar-day",
                ColorClass = "btn-danger"
            }
        };

        public static List<WorklogTimePreset> MeetingPresets => new()
        {
            new WorklogTimePreset
            {
                Name = "15min",
                Description = "Stand-up / Daily",
                TimeInMinutes = 15,
                IconClass = "fa-users",
                ColorClass = "btn-info"
            },
            new WorklogTimePreset
            {
                Name = "30min",
                Description = "Reunião rápida",
                TimeInMinutes = 30,
                IconClass = "fa-handshake",
                ColorClass = "btn-secondary"
            },
            new WorklogTimePreset
            {
                Name = "1h",
                Description = "Reunião padrão",
                TimeInMinutes = 60,
                IconClass = "fa-video",
                ColorClass = "btn-secondary"
            },
            new WorklogTimePreset
            {
                Name = "2h",
                Description = "Workshop / Treinamento",
                TimeInMinutes = 120,
                IconClass = "fa-chalkboard-teacher",
                ColorClass = "btn-secondary"
            }
        };

        public static List<WorklogTimePreset> GMPresets => new()
        {
            new WorklogTimePreset
            {
                Name = "1h",
                Description = "Suporte rápido",
                TimeInMinutes = 60,
                IconClass = "fa-headset",
                ColorClass = "btn-success"
            },
            new WorklogTimePreset
            {
                Name = "2h",
                Description = "Acompanhamento GM",
                TimeInMinutes = 120,
                IconClass = "fa-tools",
                ColorClass = "btn-success"
            },
            new WorklogTimePreset
            {
                Name = "3h",
                Description = "Investigação / Troubleshooting",
                TimeInMinutes = 180,
                IconClass = "fa-search",
                ColorClass = "btn-success"
            },
            new WorklogTimePreset
            {
                Name = "4h",
                Description = "Suporte extenso",
                TimeInMinutes = 240,
                IconClass = "fa-life-ring",
                ColorClass = "btn-success"
            }
        };

        public static List<WorklogTimePreset> ReviewPresets => new()
        {
            new WorklogTimePreset
            {
                Name = "30min",
                Description = "Code review simples",
                TimeInMinutes = 30,
                IconClass = "fa-code-branch",
                ColorClass = "btn-info"
            },
            new WorklogTimePreset
            {
                Name = "1h",
                Description = "Code review padrão",
                TimeInMinutes = 60,
                IconClass = "fa-search-plus",
                ColorClass = "btn-info"
            },
            new WorklogTimePreset
            {
                Name = "2h",
                Description = "Code review complexo",
                TimeInMinutes = 120,
                IconClass = "fa-microscope",
                ColorClass = "btn-info"
            }
        };
    }
}
