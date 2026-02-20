// Replication of the CW ABI demangler used by NVIDIA for the NVIDIA Shield ports
// written by RootCubed, ported from the C++ version:
// https://gist.github.com/RootCubed/d7e2629f4576059853505b7931ffd105

using System;
using System.Collections.Generic;

public static class NvidiaDemangler {
	public static Dictionary<char, string> BasicTypes = new Dictionary<char, string>{
		{'v', "void"},
		{'b', "bool"},
		{'c', "char"},
		{'s', "short"},
		{'i', "int"},
		{'l', "long"},
		{'f', "float"},
		{'d', "double"},
		{'w', "wchar_t"}
	};
	
	public static void parseClassOrBasicType(string mangled, ref string out_str, ref int pos) {
		if (mangled[pos] == 'Q') {
			pos++;
			parseQClass(mangled, ref out_str, ref pos);
		} else if (char.IsDigit(mangled[pos])) {
			parseSimpleClass(mangled, ref out_str, ref pos);
		} else {
			if (mangled[pos] == 'U') {
				out_str += "unsigned " + BasicTypes[mangled[++pos]];
			} else {
				out_str += BasicTypes[mangled[pos]];
			}
			pos++;
		}
	}
	public static void parseQClass(string mangled, ref string out_str, ref int pos) {
		int count = mangled[pos] - '0';
		pos++;
		for (int i = 0; i < count; i++) {
			parseSimpleClass(mangled, ref out_str, ref pos);
			if (i < count - 1) out_str += "::";
		}
	}
	public static void parseSimpleClass(string mangled, ref string out_str, ref int pos) {
		int size = 0;
		while (pos < mangled.Length && char.IsNumber(mangled[pos])) {
			size *= 10;
			size += mangled[pos] - '0';
			pos++;
		}

		int end = pos + size;
		while (pos < end) {
			char c = mangled[pos];
			out_str += c;
			pos++;
			if (c == '<') {
				// Demangler bug: The demangler assumes one template parameter only
				// Demangler bug: No checks for literal values as template arguments
				parseArgType(mangled, ref out_str, ref pos);
			}
		};
	}
	public static void parseArgType(string mangled, ref string out_str, ref int pos) {
		// Demangler bug: type modifiers are handled incorrectly
		bool isConst = false;
		bool isPtr = false;
		bool isRef = false;
		while (pos < mangled.Length) {
			char c = mangled[pos];

			// Demangler bug: M (PTMFS) and A (arrays) are not handled
			if (c == 'C') {
				isConst = true;
			} else if (c == 'P') {
				isPtr = true;
			} else if (c == 'R') {
				isRef = true;
			} else if (c == 'F') {
				// Demangler bug: Demangler was built without function pointers in mind, so they are incorrectly handled
				out_str += "( ";
				try {
					pos++;
					parseFunction(mangled, ref out_str, ref pos);
				} catch (Exception e) {
					out_str += " )";
					throw e;
				}
				out_str += " )";
				break;
			} else {
				break;
			}
			pos++;
		}
		if (isConst) out_str += "const ";
		string typeName = "";
		try {
			parseClassOrBasicType(mangled, ref typeName, ref pos);
		} catch (IndexOutOfRangeException e) {
			if (typeName != "") {
				out_str += typeName;
				if (isPtr) out_str += "*";
				if (isRef) out_str += "&";
			}
			throw e;
		}
		out_str += typeName;
		// Demangler bug: The order of R and P does not matter
		if (isPtr) out_str += "*";
		if (isRef) out_str += "&";
	}

	public static void parseFunction(string mangled, ref string out_str, ref int pos) {
		while (pos < mangled.Length) {
			parseArgType(mangled, ref out_str, ref pos);
			if (pos < mangled.Length) {
				out_str += ", ";
			}
		}
	}

	public static string Demangle(string mangled) {
		string funcName = "";
		int i = 0;
		while (i < mangled.Length) {
			funcName += mangled[i];
			i++;
			// Followed by /__[CFQ0-9]/?
			if (i <= mangled.Length - 3 && mangled[i] == '_' && mangled[i + 1] == '_') {
				char cAfter = mangled[i + 2];
				if (cAfter == 'C' || cAfter == 'F' || cAfter == 'Q' || char.IsNumber(cAfter)) break;
			}
		}
		if (i == mangled.Length) return funcName;

		// Skip past __
		i += 2;

		string res = "";

		try {
			// Check if class method or global function
			if (mangled[i] != 'F' && mangled[i] != 'C') {
				parseClassOrBasicType(mangled, ref res, ref i);
				res += "::";
			}
			res += funcName;
		} catch {}
		
		if (i == mangled.Length) return res;

		// Probably not how NVIDIA did it, but I can't get it to work by supporting const functions in parseArgType
		bool isConst = false;
		if (mangled[i] == 'C') {
			isConst = true;
			i++;
		}

		try {
			parseArgType(mangled, ref res, ref i);
		} catch {}
		if (isConst) res += " const";

		return res;
	}

	public static void Main(string[] args) {
		if (args.Length == 0) {
			string mangled = "";
			while (true) {
				mangled = Console.ReadLine();
				Console.WriteLine(Demangle(mangled));
			}
		} else {
			string mangled = args[0];
			Console.WriteLine(Demangle(mangled));
		}
	}
}
