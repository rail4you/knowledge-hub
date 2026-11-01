import { mapEnumToOptions } from '@abp/ng.core';

export enum CooperationDemandType {
  TalentTraining = 0,
  Internship = 1,
  Curriculum = 2,
  BaseSharing = 3,
  TeacherPractice = 4,
  TechTransfer = 5,
  IndustrialCollege = 6,
}

export const cooperationDemandTypeOptions = mapEnumToOptions(CooperationDemandType);
