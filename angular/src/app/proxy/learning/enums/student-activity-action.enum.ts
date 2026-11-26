import { mapEnumToOptions } from '@abp/ng.core';

export enum StudentActivityAction {
  StudyChapter = 0,
  AnswerExercise = 1,
  SubmitExam = 2,
  SubmitAssignment = 3,
  WatchVideo = 4,
  CheckIn = 5,
  PostDiscussion = 6,
  SubmitGroupTask = 7,
  Practice = 8,
}

export const studentActivityActionOptions = mapEnumToOptions(StudentActivityAction);
