const fs = require('fs');
const path = require('path');

function walk(dir) {
    let results = [];
    const list = fs.readdirSync(dir);
    list.forEach(function(file) {
        file = path.resolve(dir, file);
        const stat = fs.statSync(file);
        if (stat && stat.isDirectory()) {
            results = results.concat(walk(file));
        } else if (file.endsWith('.js') || file.endsWith('.jsx')) {
            results.push(file);
        }
    });
    return results;
}

const allFiles = walk(path.join(__dirname, 'Mobile', 'src'));

allFiles.forEach(file => {
    const content = fs.readFileSync(file, 'utf8');
    const importRegex = /from\s+['"]([^'"]+)['"]/g;
    let match;
    while ((match = importRegex.exec(content)) !== null) {
        const importPath = match[1];
        if (importPath.startsWith('.')) {
            // It's a relative import
            let resolvedPath = path.resolve(path.dirname(file), importPath);
            let exists = false;
            
            // Check possible extensions
            const exts = ['.js', '.jsx', '/index.js', '/index.jsx', '.ts', '.tsx'];
            for (let ext of exts) {
                if (fs.existsSync(resolvedPath + ext)) {
                    exists = true;
                    break;
                }
            }
            // also check if exact exists
            if (fs.existsSync(resolvedPath) && fs.statSync(resolvedPath).isFile()) {
                exists = true;
            }

            if (!exists) {
                console.log(`BROKEN IMPORT: ${importPath} in ${file.replace(__dirname, '')}`);
            }
        }
    }
});
