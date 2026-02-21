// Replication of the CW ABI demangler used by NVIDIA for the NVIDIA Shield ports
// written by RootCubed, ported from the C++ version:
// https://gist.github.com/RootCubed/d7e2629f4576059853505b7931ffd105

const BASIC_TYPES = {
	'v': 'void',
	'b': 'bool',
	'c': 'char',
	's': 'short',
	'i': 'int',
	'l': 'long',
	'f': 'float',
	'd': 'double',
	'w': 'wchar_t'
};

function isdigit(c) {
	return c >= '0' && c <= '9';
}

function parseClassOrBasicType(mangled, op) {
	if (op.pos >= mangled.length) throw new RangeError("Index out of range");
	if (mangled[op.pos] == 'Q') {
		op.pos++;
		parseQClass(mangled, op);
	} else if (isdigit(mangled[op.pos])) {
		parseSimpleClass(mangled, op);
	} else {
		if (mangled[op.pos] == 'U') {
			op.out += "unsigned " + basicTypes[mangled[++op.pos]];
		} else {
			op.out += BASIC_TYPES[mangled[op.pos]];
		}
		op.pos++;
	}
}

function parseQClass(mangled, op) {
	let count = mangled[op.pos] - '0';
	op.pos++;
	for (let i = 0; i < count; i++) {
		parseSimpleClass(mangled, op);
		if (i < count - 1) op.out += "::";
	}
}

function parseSimpleClass(mangled, op) {
	let size = 0;
	while (op.pos < mangled.length && isdigit(mangled[op.pos])) {
		size *= 10;
		size += (mangled[op.pos] - '0');
		op.pos++;
	}

	let end = op.pos + size;
	while (op.pos < end) {
		let c = mangled[op.pos];
		op.out += c;
		op.pos++;
		if (c == '<') {
			// Demangler bug: The demangler assumes one template parameter only
			// Demangler bug: No checks for literal values as template arguments
			parseArgType(mangled, op);
		}
	};
}

function parseArgType(mangled, op) {
	// Demangler bug: type modifiers are handled incorrectly
	let isConst = false;
	let isPtr = false;
	let isRef = false;
	while (op.pos < mangled.length) {
		let c = mangled[op.pos];

		// Demangler bug: M (PTMFS) and A (arrays) are not handled
		if (c == 'C') {
			isConst = true;
		} else if (c == 'P') {
			isPtr = true;
		} else if (c == 'R') {
			isRef = true;
		} else if (c == 'F') {
			// Demangler bug: Demangler was built without function pointers in mind, so they are incorrectly handled
			op.out += "( ";
			try {
				op.pos++;
				parseFunction(mangled, op);
			} catch (e) {
				op.out += " )";
			}
			op.out += " )";
			break;
		} else {
			break;
		}
		op.pos++;
	}
	if (isConst) op.out += "const ";
	let typeName = "";
	try {
		let obj = {out: typeName, pos: op.pos};
		parseClassOrBasicType(mangled, obj);
		typeName = obj.out;
		op.pos = obj.pos;
	} catch (e) {
		if (e instanceof RangeError) {
			if (typeName != "") {
				op.out += typeName;
				if (isPtr) op.out += "*";
				if (isRef) op.out += "&";
			}
		}
	}
	op.out += typeName;
	// Demangler bug: The order of R and P does not matter
	if (isPtr) op.out += "*";
	if (isRef) op.out += "&";
}

function parseFunction(mangled, op) {
	while (op.pos < mangled.length) {
		parseArgType(mangled, op);
		if (op.pos < mangled.length) {
			op.out += ", ";
		}
	}
}

function demangle(mangled) {
	let funcName = "";
	let i = 0;
	while (i < mangled.length) {
		funcName += mangled[i];
		i++;
		// Followed by /__[CFQ0-9]/?
		if (i <= mangled.length - 3 && mangled[i] == '_' && mangled[i + 1] == '_') {
			let cAfter = mangled[i + 2];
			if (cAfter == 'C' || cAfter == 'F' || cAfter == 'Q' || isdigit(cAfter)) break;
		}
	}
	if (i == mangled.length) return funcName;

	// Skip past __
	i += 2;

	let res = "";

	try {
		// Check if class method or global function
		if (mangled[i] != 'F' && mangled[i] != 'C') {
			let obj = {out: res, pos: i}
			parseClassOrBasicType(mangled, obj);
			res = obj.out;
			i = obj.pos;
			res += "::";
		}
		res += funcName;
	} catch {}
	
	if (i == mangled.length) return res;

	// Probably not how NVIDIA did it, but I can't get it to work by supporting const functions in parseArgType
	let isConst = false;
	if (mangled[i] == 'C') {
		isConst = true;
		i++;
	}

	try {
		let obj = {out: res, pos: i};
		parseArgType(mangled, obj);
		res = obj.out;
		i = obj.pos;
	} catch {}
	if (isConst) res += " const";

	return res;
}
