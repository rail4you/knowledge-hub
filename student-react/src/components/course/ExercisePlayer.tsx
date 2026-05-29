import { useState } from 'react';
import type { ExerciseDto, ExerciseType } from '../../lib/types';
import { Check, X, ChevronDown, ChevronUp, Lightbulb, Clock } from 'lucide-react';

const typeLabels: Record<number, string> = {
  0: '单选题', 1: '多选题', 2: '判断题', 3: '填空题', 4: '简答题', 5: '论述题', 6: '案例分析',
};

const difficultyLabels: Record<number, string> = {
  1: '入门', 2: '初级', 3: '中级', 4: '高级', 5: '专家',
};

interface Props {
  exercise: ExerciseDto;
  onAnswer: (exerciseId: string, answer: string) => void;
  isCorrect?: boolean | null;
  lastAnswer?: string;
}

export function ExercisePlayer({ exercise, onAnswer, isCorrect, lastAnswer }: Props) {
  const [selectedAnswer, setSelectedAnswer] = useState(lastAnswer || '');
  const [showExplanation, setShowExplanation] = useState(false);
  const [submitted, setSubmitted] = useState(isCorrect !== undefined);

  const parsedOptions: string[] = (() => {
    if (!exercise.options) return [];
    try {
      return JSON.parse(exercise.options);
    } catch {
      return exercise.options.split('\n').filter(Boolean);
    }
  })();

  const handleSubmit = () => {
    if (!selectedAnswer.trim()) return;
    onAnswer(exercise.id, selectedAnswer);
    setSubmitted(true);
  };

  const handleRetry = () => {
    setSelectedAnswer('');
    setSubmitted(false);
    setShowExplanation(false);
  };

  const renderSingleChoice = () => (
    <div className="space-y-2">
      {parsedOptions.map((opt, i) => {
        const letter = String.fromCharCode(65 + i); // A, B, C, D
        const isSelected = selectedAnswer === letter;
        let bgClass = 'bg-white hover:bg-[#F0F6FF]';
        if (submitted && isSelected && isCorrect) bgClass = 'bg-[#F6FFED] border-[#52C41A]';
        if (submitted && isSelected && isCorrect === false) bgClass = 'bg-[#FFF1F0] border-[#FF4D4F]';
        if (submitted && !isSelected && letter === exercise.answer) bgClass = 'bg-[#F6FFED] border-[#52C41A]';

        return (
          <button
            key={letter}
            disabled={submitted}
            onClick={() => setSelectedAnswer(letter)}
            className={`w-full text-left flex items-center gap-3 p-3 rounded-lg border transition-colors ${bgClass} ${
              isSelected && !submitted ? 'border-[#0056D2] ring-1 ring-[#0056D2]' : 'border-[#E8E8E8]'
            }`}
          >
            <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold border ${
              isSelected ? 'bg-[#0056D2] text-white border-[#0056D2]' : 'border-[#ccc] text-[#666]'
            }`}>
              {letter}
            </span>
            <span className="text-sm text-[#1a1a1a]">{opt}</span>
            {submitted && letter === exercise.answer && (
              <Check className="h-4 w-4 text-[#52C41A] ml-auto shrink-0" />
            )}
            {submitted && isSelected && isCorrect === false && (
              <X className="h-4 w-4 text-[#FF4D4F] ml-auto shrink-0" />
            )}
          </button>
        );
      })}
    </div>
  );

  const renderMultiChoice = () => {
    const selected = selectedAnswer.split(',').filter(Boolean);
    const toggleOption = (letter: string) => {
      const next = selected.includes(letter)
        ? selected.filter(s => s !== letter)
        : [...selected, letter].sort();
      setSelectedAnswer(next.join(','));
    };

    return (
      <div className="space-y-2">
        {parsedOptions.map((opt, i) => {
          const letter = String.fromCharCode(65 + i);
          const isSelected = selected.includes(letter);
          let bgClass = 'bg-white hover:bg-[#F0F6FF]';
          if (submitted && isSelected && exercise.answer?.includes(letter)) bgClass = 'bg-[#F6FFED] border-[#52C41A]';
          if (submitted && isSelected && !exercise.answer?.includes(letter)) bgClass = 'bg-[#FFF1F0] border-[#FF4D4F]';
          if (submitted && !isSelected && exercise.answer?.includes(letter)) bgClass = 'bg-[#FFFBE6] border-[#FAAD14]';

          return (
            <button
              key={letter}
              disabled={submitted}
              onClick={() => toggleOption(letter)}
              className={`w-full text-left flex items-center gap-3 p-3 rounded-lg border transition-colors ${bgClass} ${
                isSelected && !submitted ? 'border-[#0056D2]' : 'border-[#E8E8E8]'
              }`}
            >
              <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded text-xs font-bold border ${
                isSelected ? 'bg-[#0056D2] text-white border-[#0056D2]' : 'border-[#ccc] text-[#666]'
              }`}>
                {letter}
              </span>
              <span className="text-sm text-[#1a1a1a]">{opt}</span>
            </button>
          );
        })}
      </div>
    );
  };

  const renderTrueFalse = () => {
    const options = ['正确', '错误'];
    const letters = ['A', 'B'];

    return (
      <div className="flex gap-4">
        {options.map((opt, i) => {
          const letter = letters[i];
          const isSelected = selectedAnswer === letter;
          const correctAnswer = exercise.answer === 'A' ? '正确' : exercise.answer === 'B' ? '错误' : exercise.answer;
          let bgClass = 'bg-white hover:bg-[#F0F6FF]';
          if (submitted && isSelected && isCorrect) bgClass = 'bg-[#F6FFED] border-[#52C41A]';
          if (submitted && isSelected && isCorrect === false) bgClass = 'bg-[#FFF1F0] border-[#FF4D4F]';

          return (
            <button
              key={letter}
              disabled={submitted}
              onClick={() => setSelectedAnswer(letter)}
              className={`flex-1 flex items-center justify-center gap-2 p-4 rounded-lg border transition-colors ${bgClass} ${
                isSelected && !submitted ? 'border-[#0056D2] ring-1 ring-[#0056D2]' : 'border-[#E8E8E8]'
              }`}
            >
              <span className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold border ${
                isSelected ? 'bg-[#0056D2] text-white border-[#0056D2]' : 'border-[#ccc] text-[#666]'
              }`}>
                {letter}
              </span>
              <span className="text-sm font-medium text-[#1a1a1a]">{opt}</span>
            </button>
          );
        })}
      </div>
    );
  };

  const renderFillBlank = () => (
    <div>
      <input
        type="text"
        value={selectedAnswer}
        onChange={e => setSelectedAnswer(e.target.value)}
        disabled={submitted}
        placeholder="请输入答案..."
        className="w-full p-3 rounded-lg border border-[#E8E8E8] text-sm outline-none focus:border-[#0056D2] focus:ring-1 focus:ring-[#0056D2] disabled:bg-gray-50"
      />
    </div>
  );

  const renderShortAnswer = () => (
    <div>
      <textarea
        value={selectedAnswer}
        onChange={e => setSelectedAnswer(e.target.value)}
        disabled={submitted}
        placeholder="请输入答案..."
        rows={3}
        className="w-full p-3 rounded-lg border border-[#E8E8E8] text-sm outline-none focus:border-[#0056D2] focus:ring-1 focus:ring-[#0056D2] disabled:bg-gray-50 resize-none"
      />
    </div>
  );

  const renderAnswerArea = () => {
    switch (exercise.type) {
      case 0: return renderSingleChoice();
      case 1: return renderMultiChoice();
      case 2: return renderTrueFalse();
      case 3: return renderFillBlank();
      default: return renderShortAnswer();
    }
  };

  return (
    <div className="border border-[#E8E8E8] rounded-lg p-4 bg-white">
      {/* Header */}
      <div className="flex items-center gap-2 mb-3">
        <span className="px-2 py-0.5 rounded text-xs font-medium bg-[#E8F0FE] text-[#0056D2]">
          {typeLabels[exercise.type] || '未知'}
        </span>
        <span className="px-2 py-0.5 rounded text-xs bg-[#F5F7FA] text-[#666]">
          {difficultyLabels[exercise.difficulty] || '未知'}
        </span>
        <span className="text-xs text-[#999]">{exercise.score} 分</span>
      </div>

      {/* Question */}
      <div className="mb-4">
        <h4 className="text-sm font-semibold text-[#1a1a1a] mb-1">{exercise.title}</h4>
        {exercise.questionContent && (
          <p className="text-sm text-[#666] leading-relaxed whitespace-pre-wrap">{exercise.questionContent}</p>
        )}
      </div>

      {/* Options / Answer Area */}
      {renderAnswerArea()}

      {/* Submitted result */}
      {submitted && (
        <div className={`mt-3 p-3 rounded-lg text-sm ${
          isCorrect === null ? 'bg-[#E8F0FE] text-[#0056D2]' :
          isCorrect ? 'bg-[#F6FFED] text-[#52C41A]' : 'bg-[#FFF1F0] text-[#FF4D4F]'
        }`}>
          <div className="flex items-center gap-1.5 font-medium">
            {isCorrect === null ? <Clock className="h-4 w-4" /> :
             isCorrect ? <Check className="h-4 w-4" /> : <X className="h-4 w-4" />}
            {isCorrect === null ? '已提交，等待批改' :
             isCorrect ? '回答正确！' : `回答错误，正确答案：${exercise.answer || '略'}`}
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-2 mt-3">
        {!submitted ? (
          <button
            onClick={handleSubmit}
            disabled={!selectedAnswer.trim()}
            className="px-4 py-1.5 rounded-md bg-[#0056D2] text-white text-xs font-medium hover:bg-[#0041A8] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            提交答案
          </button>
        ) : (
          <button
            onClick={handleRetry}
            className="px-4 py-1.5 rounded-md border border-[#E8E8E8] text-xs text-[#666] hover:border-[#0056D2] hover:text-[#0056D2] transition-colors"
          >
            重新作答
          </button>
        )}

        {submitted && exercise.answerExplanation && (
          <button
            onClick={() => setShowExplanation(!showExplanation)}
            className="inline-flex items-center gap-1 px-3 py-1.5 rounded-md text-xs text-[#0056D2] hover:bg-[#E8F0FE] transition-colors"
          >
            <Lightbulb className="h-3.5 w-3.5" />
            查看解析
            {showExplanation ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
          </button>
        )}
      </div>

      {/* Explanation */}
      {submitted && showExplanation && exercise.answerExplanation && (
        <div className="mt-3 p-3 rounded-lg bg-[#FFFBE6] border border-[#FFE58F] text-sm text-[#8c6900]">
          <div className="font-medium mb-1">📖 答案解析</div>
          <div className="whitespace-pre-wrap">{exercise.answerExplanation}</div>
        </div>
      )}
    </div>
  );
}
