export enum ResourceType {
  Document = 0,
  Video = 1,
  Audio = 2,
  Image = 3,
  PPT = 4,
}

export const resourceTypeOptions: { value: ResourceType; label: string }[] = [
  { value: ResourceType.Document, label: 'Enum:ResourceType.Document' },
  { value: ResourceType.Video, label: 'Enum:ResourceType.Video' },
  { value: ResourceType.Audio, label: 'Enum:ResourceType.Audio' },
  { value: ResourceType.Image, label: 'Enum:ResourceType.Image' },
  { value: ResourceType.PPT, label: 'Enum:ResourceType.PPT' },
];

export enum ResourceStatus {
  Draft = 0,
  PendingReview = 1,
  SchoolApproved = 2,
  LeagueApproved = 3,
  Rejected = 4,
  Hidden = 5,
}

export const resourceStatusOptions: { value: ResourceStatus; label: string }[] = [
  { value: ResourceStatus.Draft, label: 'Enum:ResourceStatus.Draft' },
  { value: ResourceStatus.PendingReview, label: 'Enum:ResourceStatus.PendingReview' },
  { value: ResourceStatus.SchoolApproved, label: 'Enum:ResourceStatus.SchoolApproved' },
  { value: ResourceStatus.LeagueApproved, label: 'Enum:ResourceStatus.LeagueApproved' },
  { value: ResourceStatus.Rejected, label: 'Enum:ResourceStatus.Rejected' },
  { value: ResourceStatus.Hidden, label: 'Enum:ResourceStatus.Hidden' },
];

export enum AuditType {
  Initial = 0,
  Final = 1,
}

export enum AuditStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
}
