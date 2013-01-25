grammar MessageContracts;

options 
{
	language = 'CSharp2'; 
	output=AST; 
}

tokens 
{
	TypeToken;	
	CommandToken;
	EventToken;	
	MemberToken;
	BlockToken;
	DisctionaryToken;
	FragmentGroup;
	FragmentEntry;
	FragmentReference;
	ModifierDefinition;
	EntityDefinition;
	StringRepresentationToken;
	NamespaceToken;
	ExternToken;
	UsingToken;
	ComGuidToken;
	ComInterfaceGuidToken;
}

@lexer::namespace { MessageContracts }
@parser::namespace { MessageContracts }

program	
	:	declaration+
	;
	
declaration
	: modifier_declaration
	| frag_declaration
	| type_declaration
	| entity_declaration
	| namespace_declaration	
	| extern_declaration
	| using_declaration
	;

namespace_declaration
    :	NAMESPACE (ID ('.' ID)*) ';' 
    -> ^(NamespaceToken ID*);
    
using_declaration
    :	USING (ID ('.' ID)*) ';'
    -> ^(UsingToken ID*);


frag_declaration 
	: CONST ID '=' ID ID ';' -> ^(FragmentEntry ID ID ID);  
    
modifier_declaration
	: IF Modifier '=' ID (',' ID)* ';' -> ^(ModifierDefinition Modifier ID*);
	
entity_declaration
	: lc= INTERFACE ID block '{' declaration* '}' 
	-> ^(EntityDefinition[$lc,"Block"] ID block declaration*);
	
type_declaration
	: ID Modifier? block -> ^(TypeToken ID block Modifier?);
	
member 	
	:	ID ID -> ^(MemberToken ID ID)
	|	ID -> ^(FragmentReference ID)
	;

	
block
    :   lc='('
            (member (',' member)*)?
        ')' representation? comGuid? comInterfaceGuid?
        -> ^(BlockToken[$lc,"Block"] member* representation? comGuid? comInterfaceGuid?)
    ;    
    
representation
	:	EXPLICIT STRING -> ^(StringRepresentationToken STRING);
	
extern_declaration
    :   EXTERN STRING ';' -> ^(ExternToken STRING);
   
comGuid
	:	CLASS GUID -> ^(ComGuidToken GUID);

comInterfaceGuid
	:	ALIAS GUID -> ^(ComInterfaceGuidToken GUID);

NORDIC : ('\u00E6' | '\u00C6' | '\u00F8' | '\u00D8' | '\u00E5' | '\u00C5');

EXPLICIT	
	:	'explicit';
IF
	: 'if';
USING
    :	'using';
CONST
	: 'const';	
INTERFACE 	
	:	'interface';
CLASS
	:	'class';
ALIAS
	:	'alias';
NAMESPACE 
	:	'namespace';
EXTERN
    :	'extern';
    
ID  :	('a'..'z'|'A'..'Z'|'_'| NORDIC)('a'..'z'|'A'..'Z'| NORDIC | '0'..'9'|'_'|'<'|'>'|'['|']')* ;


GUID 
	:	'[Guid("' (HEX_DIGIT|'-')+ '")]';

Modifier
	: '?'
	| '!'
	| ';'
	;


INT :	'0'..'9'+;   


STRING
    :  '"' ( ESC_SEQ | ~('\\'|'"') )* '"'
    ;


fragment
HEX_DIGIT : ('0'..'9'|'a'..'f'|'A'..'F') ;

fragment
ESC_SEQ
    :   '\\' ('b'|'t'|'n'|'f'|'r'|'\"'|'\''|'\\')
    |   UNICODE_ESC
    |   OCTAL_ESC
    ;

fragment
OCTAL_ESC
    :   '\\' ('0'..'3') ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7')
    ;

fragment
UNICODE_ESC
    :   '\\' 'u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
    ;

COMMENT
    :   '//' ~('\n'|'\r')* '\r'? '\n' {$channel=HIDDEN;}
    |   '/*' ( options {greedy=false;} : . )* '*/' {$channel=HIDDEN;}
    ;

WS  :   ( ' '
        | '\t'
        | '\r'
        | '\n'
        ) {$channel=HIDDEN;}
    ;  