import { generatePerlinNoise } from "./perlinNoise";

/**
 * Generate a single large heightmap for the entire grid
 * @param rows Number of rows in the grid
 * @param cols Number of columns in the grid
 * @param cellSize Size of each cell in world units (default 500)
 * @param noiseScale Base noise scale for terrain generation
 * @param octaves Number of octaves for fractal noise
 * @param persistence Persistence value for octave amplitude decay
 * @returns Float32Array containing height values
 */
export function generateLargeHeightmap(
  rows: number,
  cols: number,
  cellSize = 500,
  noiseScale = 0.1,
  octaves = 4,
  persistence = 0.5,
): Float32Array {
  // Calculate total dimensions
  const totalWidth = cols * cellSize;
  const totalHeight = rows * cellSize;

  // Use high resolution for quality terrain
  const resolution = 128; // samples per cell
  const samplesX = cols * resolution;
  const samplesY = rows * resolution;

  const heightmap = new Float32Array(samplesX * samplesY);

  // Generate heightmap using Perlin noise
  for (let y = 0; y < samplesY; y++) {
    for (let x = 0; x < samplesX; x++) {
      const worldX = (x / samplesX) * totalWidth;
      const worldY = (y / samplesY) * totalHeight;

      let height = 0;
      let amplitude = 1;
      let frequency = noiseScale;
      let maxValue = 0;

      // Fractal noise with multiple octaves
      for (let octave = 0; octave < octaves; octave++) {
        height +=
          generatePerlinNoise(worldX * frequency, worldY * frequency) *
          amplitude;
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= 2;
      }

      // Normalize to [-1, 1] range
      height = height / maxValue;

      heightmap[y * samplesX + x] = height;
    }
  }

  return heightmap;
}

/**
 * Extract a localized heightmap section for a specific cell
 * @param largeHeightmap The full heightmap
 * @param row Cell row index
 * @param col Cell column index
 * @param gridRows Total rows in grid
 * @param gridCols Total columns in grid
 * @param resolution Samples per cell
 * @returns Float32Array containing the cell's heightmap section
 */
export function extractCellHeightmap(
  largeHeightmap: Float32Array,
  row: number,
  col: number,
  _gridRows: number,
  gridCols: number,
  resolution = 128,
): Float32Array {
  const samplesX = gridCols * resolution;
  const cellHeightmap = new Float32Array(resolution * resolution);

  const startX = col * resolution;
  const startY = row * resolution;

  for (let y = 0; y < resolution; y++) {
    for (let x = 0; x < resolution; x++) {
      const sourceX = startX + x;
      const sourceY = startY + y;
      const sourceIndex = sourceY * samplesX + sourceX;
      const targetIndex = y * resolution + x;

      cellHeightmap[targetIndex] = largeHeightmap[sourceIndex];
    }
  }

  return cellHeightmap;
}

/**
 * Update a cell's heightmap section in the large heightmap
 * Used after erosion processing to preserve changes
 * @param largeHeightmap The full heightmap to update
 * @param cellHeightmap The modified cell heightmap
 * @param row Cell row index
 * @param col Cell column index
 * @param gridRows Total rows in grid
 * @param gridCols Total columns in grid
 * @param resolution Samples per cell
 */
export function updateCellHeightmap(
  largeHeightmap: Float32Array,
  cellHeightmap: Float32Array,
  row: number,
  col: number,
  _gridRows: number,
  gridCols: number,
  resolution = 128,
): void {
  const samplesX = gridCols * resolution;
  const startX = col * resolution;
  const startY = row * resolution;

  for (let y = 0; y < resolution; y++) {
    for (let x = 0; x < resolution; x++) {
      const sourceIndex = y * resolution + x;
      const targetX = startX + x;
      const targetY = startY + y;
      const targetIndex = targetY * samplesX + targetX;

      largeHeightmap[targetIndex] = cellHeightmap[sourceIndex];
    }
  }
}

/**
 * Calculate partitioning metadata for equal-area cells
 * @param rows Number of rows in the grid
 * @param cols Number of columns in the grid
 * @param cellSize Size of each cell in world units
 * @param resolution Samples per cell
 */
export function calculatePartitioningMetadata(
  rows: number,
  cols: number,
  cellSize = 500,
  resolution = 128,
) {
  return {
    totalWidth: cols * cellSize,
    totalHeight: rows * cellSize,
    samplesX: cols * resolution,
    samplesY: rows * resolution,
    cellSize,
    resolution,
    cellSamplesX: resolution,
    cellSamplesY: resolution,
  };
}
