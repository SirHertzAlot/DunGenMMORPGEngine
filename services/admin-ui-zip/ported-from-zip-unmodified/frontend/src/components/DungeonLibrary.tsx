import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { Download, Library, Trash2 } from "lucide-react";
import { exportDungeonLibrary } from "../lib/exportDungeonLibrary";
import type { DungeonLibraryEntry } from "../types/dungeon3d";
import DungeonLibraryCard from "./DungeonLibraryCard";

interface DungeonLibraryProps {
  entries: DungeonLibraryEntry[];
  selectedId: string | null;
  onSelect: (entry: DungeonLibraryEntry) => void;
  onRemove: (id: string) => void;
  onClearAll: () => void;
}

export default function DungeonLibrary({
  entries,
  selectedId,
  onSelect,
  onRemove,
  onClearAll,
}: DungeonLibraryProps) {
  const isEmpty = entries.length === 0;

  const handleExport = () => {
    if (!isEmpty) {
      exportDungeonLibrary(entries);
    }
  };

  return (
    <div className="flex h-full flex-col">
      {/* Header */}
      <div className="flex items-center justify-between gap-2 px-4 py-3">
        <div className="flex items-center gap-2">
          <Library className="h-4 w-4 text-primary" />
          <span className="text-sm font-semibold">Dungeon Library</span>
          {entries.length > 0 && (
            <Badge variant="secondary" className="text-xs">
              {entries.length}
            </Badge>
          )}
        </div>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            className="h-7 gap-1.5 text-xs"
            onClick={handleExport}
            disabled={isEmpty}
            title={
              isEmpty
                ? "Library is empty"
                : `Export ${entries.length} dungeons as JSON`
            }
          >
            <Download className="h-3.5 w-3.5" />
            Export
          </Button>
          {!isEmpty && (
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7 text-destructive hover:text-destructive"
              onClick={onClearAll}
              title="Clear all dungeons from library"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </Button>
          )}
        </div>
      </div>

      <Separator />

      {/* Content */}
      {isEmpty ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 p-6 text-center">
          <Library className="h-8 w-8 text-muted-foreground/40" />
          <p className="text-sm text-muted-foreground">
            No dungeons in library
          </p>
          <p className="text-xs text-muted-foreground/70">
            Use batch generation to populate the library
          </p>
        </div>
      ) : (
        <ScrollArea className="flex-1">
          <div className="grid grid-cols-2 gap-2 p-3 sm:grid-cols-3 lg:grid-cols-2 xl:grid-cols-3">
            {entries.map((entry) => (
              <DungeonLibraryCard
                key={entry.id}
                entry={entry}
                isSelected={selectedId === entry.id}
                onSelect={onSelect}
                onRemove={onRemove}
              />
            ))}
          </div>
        </ScrollArea>
      )}
    </div>
  );
}
