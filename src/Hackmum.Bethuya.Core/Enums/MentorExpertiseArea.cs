namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Topic areas a mentor can offer guidance in during community mentorship sessions.
/// </summary>
public enum MentorExpertiseArea
{
    /// <summary>Software design, architecture, coding, and engineering craft.</summary>
    SoftwareEngineering = 1,
    /// <summary>Product strategy, roadmapping, and user research.</summary>
    ProductManagement = 2,
    /// <summary>Community organising, volunteer growth, and event facilitation.</summary>
    CommunityBuilding = 3,
    /// <summary>Open-source contribution, maintainer workflows, and project governance.</summary>
    OpenSource = 4,
    /// <summary>Startup building, ideation, fundraising, and go-to-market strategy.</summary>
    Entrepreneurship = 5,
    /// <summary>Academic and applied research methods and publishing.</summary>
    Research = 6,
    /// <summary>UX, UI, visual design, and design systems.</summary>
    Design = 7,
    /// <summary>Data science, machine learning, and AI engineering.</summary>
    DataAndAI = 8,
    /// <summary>DevOps, platform engineering, and cloud infrastructure.</summary>
    DevOps = 9,
    /// <summary>Career growth, job search, and professional development.</summary>
    CareerGrowth = 10,
    /// <summary>Any other expertise area not covered above.</summary>
    Other = 99
}
