import re
import logging

arg_retext = r'((in|out|ref)\s+)?(?P<arg_type>\w+)\s*(?P<is_array>\[\s*\])?\s+(?P<arg_name>\w+)'
arg_patten = re.compile(arg_retext)

class FuncArgInfo(object):
    def __init__(self):
        self.arg_type = '-'
        self.is_array = False
        self.arg_name = '-'
    def __str__(self):
        if self.is_array:
            return '%s[] %s' % (self.arg_type, self.arg_name)
        else:
            return '%s %s' % (self.arg_type, self.arg_name)

#<domain>[constraint]<return type><func_name>([argments])
func_pattern = re.compile(r'(?P<domain>public|protected|private|internal)\s+((?P<modifier>static|virtual|override)\s+)?(?P<return_type>\w+)\s+(?P<func>\w+(<\w+>)?)\s*\((?P<args>.*)\)\s*[\{\n]?$')

class FuncInfo(object):
    def __init__(self, m):
        self.text_ = m.group(0)
        d = m.groupdict()
        self.return_type = d['return_type']
        self.args_text_ = d['args']
        self.func_name = d['func']
        self.args = []
        pargs = self.args_text_.strip().split(',')
        for arg in pargs:
            mt = arg_patten.match(arg.strip())
            if mt:
                gd = mt.groupdict()
                #-
                fai = FuncArgInfo()
                fai.arg_type = gd['arg_type']
                fai.arg_name = gd['arg_name']
                if gd['is_array']:
                    fai.is_array = len(gd['is_array']) > 1
                #-
                self.args.append(fai)

    def text(self):
        return self.text_
    def arg_text(self):
        return self.arg_text_

    def __str__(self):
        if len(self.args) > 0:
            return '%s %s(%s)' % (self.return_type, self.func_name, ','.join(map(str,self.args)))
        else:
            return '%s %s()' % (self.return_type, self.func_name)
    


def parse_line(line):
    m=func_pattern.match(line.strip())
    if m:
        fi = FuncInfo(m)
        return fi
    return None



def parse_csharpsrc(filepath):
    fis = []
    with open(filepath,'rb') as f:
        for line in f:
            r=parse_line(line.decode('utf-8'))
            if r:
                fis.append(r)
    return fis



def process_csharpsrc(filepath):
    fis = parse_csharpsrc(filepath)
    f=open(filepath,'rb') 
    b=f.read()
    t=b.decode('utf-8')
    for fi in fis:
        tidx = t.find(fi.text())
        if tidx == -1:
            logging.warning("file path:%s func key words:'%s' not found in src text", filepath, fi.text())
            continue
        else:
            insidx = t.find('{', tidx+len(fi.text()))
            if insidx == -1:
                logging.warning("file pth func key worls:'%s' not found func body begin", filepath, fi.text())
                continue
            else:
                tl = list(t)
                #insert cod3
                insert_code_line = '\n/#insert code line#/\n'
                cci = 0
                for lci in insert_code_line:
                    tl.insert(insidx+1+cci, lci)
                    cci = cci + 1
                t = ''.join(tl)

    ##
    open(filepath.replace('.cs','.inj.cs'),'wb').write(t.encode('utf-8'))





#r=parse_line('public void AddBuffLua(int buffID, double time, int casterID, int[] rootcasterID, int skillID, int layer, int removeOnCasterDead, int removeOnSkillBreak)')
#print(r)

#r=parse_line('  public void f(ref int a) ')
#print(r)


#fis=parse_csharpsrc('a.cs')

process_csharpsrc('a.cs')









