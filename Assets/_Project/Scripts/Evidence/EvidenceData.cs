using System;
using UnityEngine;

[Serializable]
public sealed class EvidenceData
{
    [SerializeField] private string id;
    [SerializeField] private string evidenceName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    public string Id => id;
    public string EvidenceName => evidenceName;
    public string Description => description;

    public EvidenceData(string id, string evidenceName, string description)
    {
        this.id = id;
        this.evidenceName = evidenceName;
        this.description = description;
    }
}
