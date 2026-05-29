import { useState } from 'react';
import type { ExerciseDto } from '../../lib/types';
import { ExercisePlayer } from './ExercisePlayer';
import { ChevronDown, ChevronRight, FileText } from 'lucide-react';

interface Props {
  exercises: ExerciseDto[];
  chapterTitle?: string;
  onAnswer: (exerciseId: string, answer: string) => void;
  answeredMap: Map<string, { answer: string; isCorrect: boolean | null }>;
}

export function ExerciseList({ exercises, chapterTitle, onAnswer, answeredMap }: Props) {
  const [expanded, setExpanded] = useState(true);

  if (exercises.length === 0) {
    return (
      <div className="text-center py-8 text-sm text-[#999]">
        <FileText className="h-8 w-8 mx-auto mb-2 text-[#ccc]" />
        暂无习题
      </div>
    );
  }

  return (
    <div className="border border-[#E8E8E8] rounded-lg overflow-hidden">
      {/* Chapter header */}
      {chapterTitle && (
        <button
          onClick={() => setExpanded(!expanded)}
          className="w-full flex items-center gap-2 px-4 py-3 bg-[#F5F7FA] hover:bg-[#EDF1F7] transition-colors text-left"
        >
          {expanded ? <ChevronDown className="h-4 w-4 text-[#666]" /> : <ChevronRight className="h-4 w-4 text-[#666]" />}
          <span className="text-sm font-semibold text-[#1a1a1a]">{chapterTitle}</span>
          <span className="text-xs text-[#999] ml-auto">{exercises.length} 题</span>
        </button>
      )}

      {/* Exercise list */}
      {expanded && (
        <div className="divide-y divide-[#E8E8E8] p-4 space-y-3">
          {exercises.map((ex, index) => {
            const answered = answeredMap.get(ex.id);
            return (
              <div key={ex.id} className="pt-3 first:pt-0">
                {exercises.length > 1 && (
                  <div className="text-xs text-[#999] mb-2">第 {index + 1} 题</div>
                )}
                <ExercisePlayer
                  exercise={ex}
                  onAnswer={onAnswer}
                  isCorrect={answered?.isCorrect}
                  lastAnswer={answered?.answer}
                />
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
