Code Style Guide
1. EOF: No trailing blank lines at EOF
2. Documentation:
   - Every method and type must have XML documentation:/// <summary>
     /// Description without a trailing period
     /// </summary>
   - Preserve existing <param> and <returns> tags
   - Use <inheritdoc/> for inherited or implemented members
   - No trailing periods in documentation comments
3. Design: Follow KISS, YAGNI, and SOLID
4. Consistency: Follow existing project style, naming conventions, and architecture
5. File Structure: One class per file
6. Constants:
   - Avoid hardcoded values and magic numbers/strings
   - Extract meaningful fixed values into named constants when appropriate
   - Keep class-specific constants within the class
   - For constants shared across multiple classes, use a dedicated static constants class
   - Use configuration instead of constants for environment-dependent values
7. Performance: Avoid unnecessary allocations, repeated enumeration, and redundant database queries; prioritize readability over micro-optimizations
8. Nullability: Respect nullable reference types; avoid using ! to hide potential null issues
9. Validation: Validate inputs at system boundaries; avoid redundant checks
10. Compatibility: Preserve existing behavior, public contracts, and backward compatibility unless explicitly requested otherwise
11. Build: Do not run project builds, Docker builds, or Dockerfile builds unless explicitly requested