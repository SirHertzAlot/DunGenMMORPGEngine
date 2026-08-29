import type { GridConfig } from '../types/grid';

interface GridExportData {
  metadata: {
    exportDate: string;
    version: string;
    description: string;
  };
  grid: {
    id: string;
    dimension: number;
    totalCells: number;
  };
  cells: Array<{
    position: { row: number; col: number };
    config: GridConfig['cells'][0][0];
    adjacency: {
      top: boolean;
      bottom: boolean;
      left: boolean;
      right: boolean;
    };
  }>;
}

export function exportGridData(gridConfig: GridConfig): void {
  const { id, dimension, cells } = gridConfig;

  const exportData: GridExportData = {
    metadata: {
      exportDate: new Date().toISOString(),
      version: '2.0.0',
      description: '3D Terrain Grid Builder - Complete grid configuration with cell-based terrain data',
    },
    grid: {
      id,
      dimension,
      totalCells: dimension * dimension,
    },
    cells: [],
  };

  // Export each cell with position and adjacency information
  for (let row = 0; row < dimension; row++) {
    for (let col = 0; col < dimension; col++) {
      exportData.cells.push({
        position: { row, col },
        config: cells[row][col],
        adjacency: {
          top: row > 0,
          bottom: row < dimension - 1,
          left: col > 0,
          right: col < dimension - 1,
        },
      });
    }
  }

  const content = JSON.stringify(exportData, null, 2);
  const filename = `terrain_grid_${id}_${Date.now()}.json`;
  const mimeType = 'application/json';

  // Create and trigger download
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
