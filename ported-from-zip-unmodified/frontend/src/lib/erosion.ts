// Erosion simulation algorithms for terrain modification

export interface HydraulicErosionParams {
  iterations: number;
  strength: number;
  sedimentCapacity: number;
}

export interface ThermalErosionParams {
  iterations: number;
  strength: number;
  talusAngle: number;
}

export interface WindErosionParams {
  iterations: number;
  direction: number;
  transportRate: number;
}

export interface PlateauErosionParams {
  iterations: number;
  threshold: number;
  strength: number;
}

export interface RiverErosionParams {
  iterations: number;
  flowDirectionBias: number;
  rainfallSourcePoints: number;
  erosionDepositionRate: number;
  evaporationRate: number;
  poolingThreshold: number;
}

export interface RiverData {
  waterDepth: Float32Array;
  flowVectors: Float32Array;
  poolingBasins: Float32Array;
}

/**
 * Apply hydraulic erosion to terrain height map
 * Simulates water flow, erosion, sediment transport, and deposition
 * Works on a cloned array to prevent data corruption
 */
export function applyHydraulicErosion(
  positions: Float32Array,
  segments: number,
  params: HydraulicErosionParams,
  onProgress?: (progress: number) => void
): Float32Array {
  const size = segments + 1;
  const heights = new Float32Array(size * size);
  
  // Extract heights from positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    heights[index] = positions[i + 2];
  }

  const { iterations, strength, sedimentCapacity } = params;
  
  for (let iter = 0; iter < iterations; iter++) {
    // Random droplet starting position
    let x = Math.random() * (size - 1);
    let y = Math.random() * (size - 1);
    
    let sediment = 0;
    let water = 1;
    let velocity = 0;
    
    // Simulate droplet path
    for (let step = 0; step < 30; step++) {
      const xi = Math.floor(x);
      const yi = Math.floor(y);
      
      if (xi < 0 || xi >= size - 1 || yi < 0 || yi >= size - 1) break;
      
      // Get current height
      const currentHeight = getHeight(heights, xi, yi, size);
      
      // Calculate gradient
      const gradient = calculateGradient(heights, xi, yi, size);
      
      // Update velocity and water
      velocity = Math.sqrt(velocity * velocity + gradient.magnitude * 9.81);
      water *= 0.98; // Evaporation
      
      // Calculate sediment capacity
      const capacity = Math.max(0, velocity * water * sedimentCapacity * strength);
      
      // Erosion or deposition
      if (sediment > capacity) {
        // Deposit sediment
        const deposit = Math.min(sediment - capacity, sediment) * 0.3;
        setHeight(heights, xi, yi, size, currentHeight + deposit);
        sediment -= deposit;
      } else {
        // Erode terrain
        const erosion = Math.min((capacity - sediment) * 0.3, currentHeight * 0.1) * strength;
        setHeight(heights, xi, yi, size, currentHeight - erosion);
        sediment += erosion;
      }
      
      // Move droplet
      x += gradient.x * velocity * 0.1;
      y += gradient.y * velocity * 0.1;
      
      if (water < 0.01) break;
    }
    
    // Report progress
    if (onProgress && iter % 10 === 0) {
      onProgress((iter + 1) / iterations);
    }
  }
  
  // Update positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    positions[i + 2] = heights[index];
  }
  
  return positions;
}

/**
 * Apply thermal erosion to terrain height map
 * Redistributes material from steep slopes based on angle threshold
 * Works on a cloned array to prevent data corruption
 */
export function applyThermalErosion(
  positions: Float32Array,
  segments: number,
  params: ThermalErosionParams,
  onProgress?: (progress: number) => void
): Float32Array {
  const size = segments + 1;
  const heights = new Float32Array(size * size);
  
  // Extract heights from positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    heights[index] = positions[i + 2];
  }

  const { iterations, strength, talusAngle } = params;
  
  for (let iter = 0; iter < iterations; iter++) {
    // Create a separate buffer for the new heights to avoid read-write conflicts
    const newHeights = new Float32Array(heights);
    
    for (let y = 1; y < size - 1; y++) {
      for (let x = 1; x < size - 1; x++) {
        const currentHeight = getHeight(heights, x, y, size);
        
        // Check all 8 neighbors
        const neighbors = [
          { dx: -1, dy: -1 }, { dx: 0, dy: -1 }, { dx: 1, dy: -1 },
          { dx: -1, dy: 0 },                      { dx: 1, dy: 0 },
          { dx: -1, dy: 1 },  { dx: 0, dy: 1 },  { dx: 1, dy: 1 }
        ];
        
        let totalDiff = 0;
        const diffs: number[] = [];
        
        // Calculate height differences
        for (const neighbor of neighbors) {
          const nx = x + neighbor.dx;
          const ny = y + neighbor.dy;
          const neighborHeight = getHeight(heights, nx, ny, size);
          const diff = currentHeight - neighborHeight;
          
          // Only consider downward slopes steeper than talus angle
          if (diff > talusAngle) {
            diffs.push(diff);
            totalDiff += diff;
          } else {
            diffs.push(0);
          }
        }
        
        // Redistribute material
        if (totalDiff > 0) {
          const maxMove = totalDiff * 0.5 * strength;
          
          for (let i = 0; i < neighbors.length; i++) {
            if (diffs[i] > 0) {
              const nx = x + neighbors[i].dx;
              const ny = y + neighbors[i].dy;
              const transfer = (diffs[i] / totalDiff) * maxMove;
              
              const currentIdx = y * size + x;
              const neighborIdx = ny * size + nx;
              
              newHeights[currentIdx] -= transfer;
              newHeights[neighborIdx] += transfer;
            }
          }
        }
      }
    }
    
    // Copy new heights back to the main buffer
    for (let i = 0; i < heights.length; i++) {
      heights[i] = newHeights[i];
    }
    
    // Report progress
    if (onProgress && iter % 10 === 0) {
      onProgress((iter + 1) / iterations);
    }
  }
  
  // Update positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    positions[i + 2] = heights[index];
  }
  
  return positions;
}

/**
 * Apply wind erosion to terrain height map
 * Simulates wind-driven sediment transport with directional effects
 * Works on a cloned array to prevent data corruption
 */
export function applyWindErosion(
  positions: Float32Array,
  segments: number,
  params: WindErosionParams,
  onProgress?: (progress: number) => void
): Float32Array {
  const size = segments + 1;
  const heights = new Float32Array(size * size);
  
  // Extract heights from positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    heights[index] = positions[i + 2];
  }

  const { iterations, direction, transportRate } = params;
  
  // Convert direction to radians and calculate wind vector
  const dirRad = (direction * Math.PI) / 180;
  const windX = Math.cos(dirRad);
  const windY = Math.sin(dirRad);
  
  for (let iter = 0; iter < iterations; iter++) {
    const newHeights = new Float32Array(heights);
    
    for (let y = 1; y < size - 1; y++) {
      for (let x = 1; x < size - 1; x++) {
        const currentHeight = getHeight(heights, x, y, size);
        
        // Calculate exposure to wind (higher points are more exposed)
        const avgNeighborHeight = getAverageNeighborHeight(heights, x, y, size);
        const exposure = Math.max(0, currentHeight - avgNeighborHeight);
        
        // Calculate erosion amount based on exposure and transport rate
        const erosionAmount = exposure * transportRate * 0.05;
        
        if (erosionAmount > 0) {
          // Erode from current position
          const currentIdx = y * size + x;
          newHeights[currentIdx] -= erosionAmount;
          
          // Deposit in downwind direction
          const depositX = Math.round(x + windX * 2);
          const depositY = Math.round(y + windY * 2);
          
          if (depositX >= 0 && depositX < size && depositY >= 0 && depositY < size) {
            const depositIdx = depositY * size + depositX;
            newHeights[depositIdx] += erosionAmount * 0.8; // Some sediment is lost
          }
        }
      }
    }
    
    // Copy new heights back
    for (let i = 0; i < heights.length; i++) {
      heights[i] = newHeights[i];
    }
    
    // Report progress
    if (onProgress && iter % 10 === 0) {
      onProgress((iter + 1) / iterations);
    }
  }
  
  // Update positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    positions[i + 2] = heights[index];
  }
  
  return positions;
}

/**
 * Apply plateau erosion to terrain height map
 * Flattens elevated areas based on height thresholds
 * Works on a cloned array to prevent data corruption
 */
export function applyPlateauErosion(
  positions: Float32Array,
  segments: number,
  params: PlateauErosionParams,
  elevation: number,
  onProgress?: (progress: number) => void
): Float32Array {
  const size = segments + 1;
  const heights = new Float32Array(size * size);
  
  // Extract heights from positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    heights[index] = positions[i + 2];
  }

  const { iterations, threshold, strength } = params;
  
  // Calculate absolute threshold height
  const thresholdHeight = threshold * elevation;
  
  for (let iter = 0; iter < iterations; iter++) {
    const newHeights = new Float32Array(heights);
    
    for (let y = 1; y < size - 1; y++) {
      for (let x = 1; x < size - 1; x++) {
        const currentHeight = getHeight(heights, x, y, size);
        
        // Only process points above threshold
        if (currentHeight > thresholdHeight) {
          // Calculate average height of neighbors above threshold
          const neighbors = [
            { dx: -1, dy: -1 }, { dx: 0, dy: -1 }, { dx: 1, dy: -1 },
            { dx: -1, dy: 0 },                      { dx: 1, dy: 0 },
            { dx: -1, dy: 1 },  { dx: 0, dy: 1 },  { dx: 1, dy: 1 }
          ];
          
          let totalHeight = currentHeight;
          let count = 1;
          
          for (const neighbor of neighbors) {
            const nx = x + neighbor.dx;
            const ny = y + neighbor.dy;
            const neighborHeight = getHeight(heights, nx, ny, size);
            
            if (neighborHeight > thresholdHeight) {
              totalHeight += neighborHeight;
              count++;
            }
          }
          
          const avgHeight = totalHeight / count;
          
          // Flatten towards average with strength parameter
          const currentIdx = y * size + x;
          newHeights[currentIdx] = currentHeight + (avgHeight - currentHeight) * strength * 0.3;
        }
      }
    }
    
    // Copy new heights back
    for (let i = 0; i < heights.length; i++) {
      heights[i] = newHeights[i];
    }
    
    // Report progress
    if (onProgress && iter % 10 === 0) {
      onProgress((iter + 1) / iterations);
    }
  }
  
  // Update positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    positions[i + 2] = heights[index];
  }
  
  return positions;
}

/**
 * Apply river erosion to terrain height map
 * Simulates water flow, pooling, and sediment transport to form lakes and riverbeds
 * Works on a cloned array to prevent data corruption
 */
export function applyRiverErosion(
  positions: Float32Array,
  segments: number,
  params: RiverErosionParams,
  onProgress?: (progress: number) => void
): RiverData {
  const size = segments + 1;
  const heights = new Float32Array(size * size);
  
  // Extract heights from positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    heights[index] = positions[i + 2];
  }

  const { iterations, flowDirectionBias, rainfallSourcePoints, erosionDepositionRate, evaporationRate, poolingThreshold } = params;
  
  // Initialize river data tracking
  const waterDepth = new Float32Array(size * size);
  const flowVectors = new Float32Array(size * size * 2); // x, y components
  const poolingBasins = new Float32Array(size * size);
  const waterAccumulation = new Float32Array(size * size);
  
  // Convert flow direction bias to radians
  const biasRad = (flowDirectionBias * Math.PI) / 180;
  const biasX = Math.cos(biasRad);
  const biasY = Math.sin(biasRad);
  
  // Generate rainfall source points
  const sources: Array<{ x: number; y: number }> = [];
  for (let i = 0; i < rainfallSourcePoints; i++) {
    sources.push({
      x: Math.random() * (size - 1),
      y: Math.random() * (size - 1)
    });
  }
  
  // Simulate water flow from each source
  for (let iter = 0; iter < iterations; iter++) {
    for (const source of sources) {
      let x = source.x;
      let y = source.y;
      let water = 1.0;
      let sediment = 0;
      let velocity = 0;
      
      // Trace water path
      for (let step = 0; step < 100; step++) {
        const xi = Math.floor(x);
        const yi = Math.floor(y);
        
        if (xi < 1 || xi >= size - 1 || yi < 1 || yi >= size - 1) break;
        
        const currentHeight = getHeight(heights, xi, yi, size);
        const idx = yi * size + xi;
        
        // Accumulate water at this position
        waterAccumulation[idx] += water * 0.1;
        
        // Calculate gradient with directional bias
        const gradient = calculateGradient(heights, xi, yi, size);
        
        // Apply flow direction bias
        const flowX = gradient.x * 0.7 + biasX * 0.3;
        const flowY = gradient.y * 0.7 + biasY * 0.3;
        const flowMag = Math.sqrt(flowX * flowX + flowY * flowY);
        
        // Store flow vector
        flowVectors[idx * 2] = flowMag > 0 ? flowX / flowMag : 0;
        flowVectors[idx * 2 + 1] = flowMag > 0 ? flowY / flowMag : 0;
        
        // Update velocity
        velocity = Math.sqrt(velocity * velocity + gradient.magnitude * 9.81);
        
        // Check for pooling (local minimum or very flat area)
        const isPooling = gradient.magnitude < 0.05 || waterAccumulation[idx] > poolingThreshold * 10;
        
        if (isPooling) {
          poolingBasins[idx] = Math.min(1.0, poolingBasins[idx] + 0.1);
          // Water pools here, deposit sediment
          if (sediment > 0) {
            setHeight(heights, xi, yi, size, currentHeight + sediment * erosionDepositionRate * 0.2);
            sediment = 0;
          }
          // Reduce velocity in pools
          velocity *= 0.5;
        }
        
        // Calculate erosion/deposition
        const capacity = velocity * water * erosionDepositionRate;
        
        if (sediment < capacity && !isPooling) {
          // Erode terrain
          const erosion = Math.min((capacity - sediment) * 0.2, currentHeight * 0.05) * erosionDepositionRate;
          setHeight(heights, xi, yi, size, currentHeight - erosion);
          sediment += erosion;
        } else if (sediment > capacity) {
          // Deposit sediment
          const deposit = (sediment - capacity) * 0.3;
          setHeight(heights, xi, yi, size, currentHeight + deposit);
          sediment -= deposit;
        }
        
        // Apply evaporation
        water *= (1 - evaporationRate * 0.02);
        
        if (water < 0.01) break;
        
        // Move water
        if (flowMag > 0) {
          x += (flowX / flowMag) * velocity * 0.1;
          y += (flowY / flowMag) * velocity * 0.1;
        } else {
          break; // Stuck in local minimum
        }
      }
    }
    
    // Report progress
    if (onProgress && iter % 10 === 0) {
      onProgress((iter + 1) / iterations);
    }
  }
  
  // Calculate final water depth based on accumulation
  for (let i = 0; i < waterAccumulation.length; i++) {
    waterDepth[i] = Math.min(1.0, waterAccumulation[i] / (iterations * rainfallSourcePoints * 0.1));
  }
  
  // Update positions array
  for (let i = 0; i < positions.length; i += 3) {
    const index = i / 3;
    positions[i + 2] = heights[index];
  }
  
  return {
    waterDepth,
    flowVectors,
    poolingBasins
  };
}

// Helper functions
function getHeight(heights: Float32Array, x: number, y: number, size: number): number {
  const index = y * size + x;
  return heights[index] || 0;
}

function setHeight(heights: Float32Array, x: number, y: number, size: number, value: number): void {
  const index = y * size + x;
  heights[index] = value;
}

function calculateGradient(heights: Float32Array, x: number, y: number, size: number): { x: number; y: number; magnitude: number } {
  const h = getHeight(heights, x, y, size);
  const hx = getHeight(heights, x + 1, y, size);
  const hy = getHeight(heights, x, y + 1, size);
  
  const dx = hx - h;
  const dy = hy - h;
  const magnitude = Math.sqrt(dx * dx + dy * dy);
  
  return {
    x: magnitude > 0 ? dx / magnitude : 0,
    y: magnitude > 0 ? dy / magnitude : 0,
    magnitude
  };
}

function getAverageNeighborHeight(heights: Float32Array, x: number, y: number, size: number): number {
  const neighbors = [
    { dx: -1, dy: -1 }, { dx: 0, dy: -1 }, { dx: 1, dy: -1 },
    { dx: -1, dy: 0 },                      { dx: 1, dy: 0 },
    { dx: -1, dy: 1 },  { dx: 0, dy: 1 },  { dx: 1, dy: 1 }
  ];
  
  let total = 0;
  let count = 0;
  
  for (const neighbor of neighbors) {
    const nx = x + neighbor.dx;
    const ny = y + neighbor.dy;
    
    if (nx >= 0 && nx < size && ny >= 0 && ny < size) {
      total += getHeight(heights, nx, ny, size);
      count++;
    }
  }
  
  return count > 0 ? total / count : 0;
}
