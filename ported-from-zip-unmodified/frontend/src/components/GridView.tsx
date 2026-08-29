import { Card } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { GridConfig } from '../types/grid';

interface GridViewProps {
  config: GridConfig;
  selectedCell: { row: number; col: number } | null;
  onCellClick: (row: number, col: number) => void;
}

export default function GridView({ config, selectedCell, onCellClick }: GridViewProps) {
  const { dimension, cells } = config;

  // Calculate responsive cell size based on dimension and viewport
  const getCellSize = () => {
    if (dimension <= 3) return 'min-w-[120px] min-h-[120px]';
    if (dimension <= 5) return 'min-w-[100px] min-h-[100px]';
    if (dimension <= 7) return 'min-w-[80px] min-h-[80px]';
    return 'min-w-[60px] min-h-[60px]';
  };

  const getGap = () => {
    if (dimension <= 3) return 'gap-4';
    if (dimension <= 5) return 'gap-3';
    if (dimension <= 7) return 'gap-2';
    return 'gap-1.5';
  };

  const getPadding = () => {
    if (dimension <= 3) return 'p-4';
    if (dimension <= 5) return 'p-3';
    if (dimension <= 7) return 'p-2';
    return 'p-1.5';
  };

  const getTextSize = () => {
    if (dimension <= 5) return 'text-xs';
    if (dimension <= 7) return 'text-[10px]';
    return 'text-[9px]';
  };

  return (
    <div className="flex h-full w-full items-center justify-center overflow-auto p-2 sm:p-4 md:p-6 lg:p-8">
      <div className="w-full max-w-full">
        <div
          className={cn(
            'mx-auto grid w-fit',
            getGap()
          )}
          style={{
            gridTemplateColumns: `repeat(${dimension}, minmax(0, 1fr))`,
          }}
        >
          {cells.map((row, rowIndex) =>
            row.map((cell, colIndex) => {
              const isSelected =
                selectedCell?.row === rowIndex && selectedCell?.col === colIndex;

              return (
                <Card
                  key={`${rowIndex}-${colIndex}`}
                  className={cn(
                    'relative aspect-square cursor-pointer transition-all hover:scale-105 hover:shadow-lg active:scale-95',
                    getCellSize(),
                    isSelected && 'ring-2 ring-primary ring-offset-2 ring-offset-background'
                  )}
                  onClick={() => onCellClick(rowIndex, colIndex)}
                >
                  <div className={cn('flex h-full flex-col items-center justify-center', getPadding())}>
                    <div className={cn('mb-1 font-mono text-muted-foreground', getTextSize())}>
                      [{rowIndex}, {colIndex}]
                    </div>
                    <div className="text-center">
                      <div className={cn('text-muted-foreground', getTextSize())}>
                        S: {cell.noiseSettings.noiseScale.toFixed(2)}
                      </div>
                      <div className={cn('text-muted-foreground', getTextSize())}>
                        E: {cell.noiseSettings.elevation.toFixed(1)}
                      </div>
                    </div>
                    <div className={cn(
                      'mt-1 w-full rounded border border-border bg-gradient-to-b from-muted to-background',
                      dimension <= 5 ? 'h-12' : dimension <= 7 ? 'h-8' : 'h-6'
                    )} />
                  </div>
                </Card>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
