import { debugLogger } from './debugLogger';

/**
 * Entity configuration interface for visualizer
 */
export interface EntityConfig {
  name: string;
  type: string;
  models: Array<{
    path: string;
    material?: string;
  }>;
  transform?: {
    position: [number, number, number];
    rotation: [number, number, number];
    scale: [number, number, number];
  };
  metadata?: Record<string, any>;
}

/**
 * Lightweight YAML parser for entity configurations
 * Supports basic YAML structures with models, transforms, materials, and metadata
 */
export function parseYAML(yamlContent: string): any {
  try {
    // Remove comments
    const lines = yamlContent.split('\n').filter(line => !line.trim().startsWith('#'));
    const cleanYAML = lines.join('\n');

    // Simple YAML parsing (supports basic key-value pairs and nested objects)
    const result: any = {};
    const stack: any[] = [{ obj: result, indent: -1 }];

    lines.forEach((line) => {
      if (!line.trim() || line.trim().startsWith('#')) return;

      const indent = line.search(/\S/);
      const trimmed = line.trim();

      // Handle list items
      if (trimmed.startsWith('- ')) {
        const value = trimmed.substring(2).trim();
        const current = stack[stack.length - 1];
        if (!Array.isArray(current.obj)) {
          current.obj = [];
        }
        if (value.includes(':')) {
          const obj: any = {};
          current.obj.push(obj);
          stack.push({ obj, indent });
        } else {
          current.obj.push(parseValue(value));
        }
        return;
      }

      // Handle key-value pairs
      const colonIndex = trimmed.indexOf(':');
      if (colonIndex === -1) return;

      const key = trimmed.substring(0, colonIndex).trim();
      const valueStr = trimmed.substring(colonIndex + 1).trim();

      // Pop stack to correct level
      while (stack.length > 1 && stack[stack.length - 1].indent >= indent) {
        stack.pop();
      }

      const current = stack[stack.length - 1].obj;

      if (valueStr === '' || valueStr === '{}' || valueStr === '[]') {
        // Empty object or array
        const newObj = valueStr === '[]' ? [] : {};
        current[key] = newObj;
        stack.push({ obj: newObj, indent });
      } else {
        // Value present
        current[key] = parseValue(valueStr);
      }
    });

    debugLogger.success('yaml-parser', 'YAML parsed successfully');
    return result;
  } catch (error: any) {
    debugLogger.error('yaml-parser', `YAML parsing failed: ${error.message}`);
    throw new Error(`YAML parsing error: ${error.message}`);
  }
}

function parseValue(value: string): any {
  // Remove quotes
  if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
    return value.slice(1, -1);
  }

  // Parse numbers
  if (!isNaN(Number(value))) {
    return Number(value);
  }

  // Parse booleans
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (value === 'null') return null;

  // Parse arrays
  if (value.startsWith('[') && value.endsWith(']')) {
    const items = value.slice(1, -1).split(',').map(item => parseValue(item.trim()));
    return items;
  }

  return value;
}

/**
 * Parse YAML configuration for entity visualization
 */
export function parseYAMLConfig(yamlContent: string, entityType: string): EntityConfig {
  try {
    const parsed = parseYAML(yamlContent);

    // Extract entity configuration
    const config: EntityConfig = {
      name: parsed.name || 'Unnamed Entity',
      type: parsed.type || entityType,
      models: [],
      transform: {
        position: [0, 0, 0],
        rotation: [0, 0, 0],
        scale: [1, 1, 1],
      },
      metadata: {},
    };

    // Parse models
    if (parsed.models) {
      if (Array.isArray(parsed.models)) {
        config.models = parsed.models.map((model: any) => {
          if (typeof model === 'string') {
            return { path: model };
          }
          return {
            path: model.path || model.file || '',
            material: model.material,
          };
        });
      } else if (typeof parsed.models === 'string') {
        config.models = [{ path: parsed.models }];
      }
    }

    // Parse transform
    if (parsed.transform) {
      if (parsed.transform.position) {
        const pos = Array.isArray(parsed.transform.position)
          ? parsed.transform.position
          : [parsed.transform.position.x || 0, parsed.transform.position.y || 0, parsed.transform.position.z || 0];
        config.transform!.position = [pos[0] || 0, pos[1] || 0, pos[2] || 0];
      }
      if (parsed.transform.rotation) {
        const rot = Array.isArray(parsed.transform.rotation)
          ? parsed.transform.rotation
          : [parsed.transform.rotation.x || 0, parsed.transform.rotation.y || 0, parsed.transform.rotation.z || 0];
        config.transform!.rotation = [rot[0] || 0, rot[1] || 0, rot[2] || 0];
      }
      if (parsed.transform.scale) {
        const scl = Array.isArray(parsed.transform.scale)
          ? parsed.transform.scale
          : [parsed.transform.scale.x || 1, parsed.transform.scale.y || 1, parsed.transform.scale.z || 1];
        config.transform!.scale = [scl[0] || 1, scl[1] || 1, scl[2] || 1];
      }
    }

    // Parse metadata
    if (parsed.metadata) {
      config.metadata = parsed.metadata;
    }

    // Include any additional properties as metadata
    Object.keys(parsed).forEach(key => {
      if (!['name', 'type', 'models', 'transform', 'metadata'].includes(key)) {
        config.metadata![key] = parsed[key];
      }
    });

    debugLogger.success('yaml-parser', `Entity configuration parsed for ${entityType}`);
    return config;
  } catch (error: any) {
    debugLogger.error('yaml-parser', `Entity config parsing failed: ${error.message}`);
    throw new Error(`Entity configuration parsing error: ${error.message}`);
  }
}

/**
 * Merge partial YAML with template YAML
 */
export function mergeYAML(template: string, partial: string): string {
  try {
    const templateObj = parseYAML(template);
    const partialObj = parseYAML(partial);

    const merged = deepMerge(templateObj, partialObj);
    const yamlOutput = objectToYAML(merged);

    debugLogger.success('yaml-parser', 'YAML merged successfully');
    return yamlOutput;
  } catch (error: any) {
    debugLogger.error('yaml-parser', `YAML merging failed: ${error.message}`);
    throw new Error(`YAML merging error: ${error.message}`);
  }
}

function deepMerge(target: any, source: any): any {
  if (Array.isArray(target) && Array.isArray(source)) {
    return [...target, ...source];
  }

  if (typeof target === 'object' && typeof source === 'object' && target !== null && source !== null) {
    const result = { ...target };
    for (const key in source) {
      if (source.hasOwnProperty(key)) {
        if (target.hasOwnProperty(key)) {
          result[key] = deepMerge(target[key], source[key]);
        } else {
          result[key] = source[key];
        }
      }
    }
    return result;
  }

  return source !== undefined ? source : target;
}

function objectToYAML(obj: any, indent: number = 0): string {
  const spaces = '  '.repeat(indent);
  let yaml = '';

  if (Array.isArray(obj)) {
    obj.forEach(item => {
      if (typeof item === 'object' && item !== null) {
        yaml += `${spaces}- \n${objectToYAML(item, indent + 1)}`;
      } else {
        yaml += `${spaces}- ${formatValue(item)}\n`;
      }
    });
  } else if (typeof obj === 'object' && obj !== null) {
    Object.entries(obj).forEach(([key, value]) => {
      if (Array.isArray(value)) {
        yaml += `${spaces}${key}:\n`;
        value.forEach(item => {
          if (typeof item === 'object' && item !== null) {
            yaml += `${spaces}  - \n${objectToYAML(item, indent + 2)}`;
          } else {
            yaml += `${spaces}  - ${formatValue(item)}\n`;
          }
        });
      } else if (typeof value === 'object' && value !== null) {
        yaml += `${spaces}${key}:\n${objectToYAML(value, indent + 1)}`;
      } else {
        yaml += `${spaces}${key}: ${formatValue(value)}\n`;
      }
    });
  }

  return yaml;
}

function formatValue(value: any): string {
  if (typeof value === 'string') {
    return value.includes(' ') || value.includes(':') ? `"${value}"` : value;
  }
  if (value === null) return 'null';
  return String(value);
}

/**
 * Visualizer Service API for external module integration
 */
export class VisualizerService {
  /**
   * Visualize an entity with specific type and configuration
   */
  static async visualize(entityType: string, configPath: string): Promise<void> {
    debugLogger.info('visualizer-service', `Visualizing ${entityType} from ${configPath}`);
    // This would be implemented to trigger the Generic Visualizer
    // For now, it's a stub for the service API
  }

  /**
   * Load entities from a directory
   */
  static async loadFromDirectory(directoryPath: string): Promise<any[]> {
    debugLogger.info('visualizer-service', `Loading entities from ${directoryPath}`);
    // This would be implemented to load and parse entities from a directory
    // For now, it's a stub for the service API
    return [];
  }
}
