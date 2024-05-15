El fin de utilizar esto es el siguiente cuando tenemos un inconveniente en produccion y lo queremos solucionar, queremos simular el mismo 
escenario o lo mas parecido posible para ello obtenemos una entidad y todas sus relaciones.
Aclaro este es un fragmento de codigo no es el codigo completo.

La idea de este proyecto es la siguiente extraer datos con nhibernate de una conexion remota
y almacenarlos en una conexion local.

El concepto es el siguiente a traves de una entidad, se extrae la misma entidad y todas sus relaciones
Se podria hacer con recursividad pero si tiene muchas relaciones se produce StackOverflow, para ello la solucion es
utilizar la instruccion goto y reutilizar las variables dinamicamente segun lo necesite. 
Esto tambien nos permite volver a la entidad original y continuar copiando sus propiedades del tipo objeto / lista pero no datos primitivos
hasta llegar a la totalidad de los datos.

Tambien tiene parametros utilizando Expression para indicarle que propiedades de la entidad debe evitar copiar,
en este caso algo recomendado para evitar persistir seria una entidad que tiene un blob con documentos ya que demoraria mucho tiempo
realizar la querie y almacenarlo localmente.

Una vez obtenidos los datos se cierra la conexion remota
Se abre una conexion local y se empiezan a persistir los datos localmente manteniendo el mismo id.

Cuando ya tenemos los datos extraidos podemos proceder a simular escenarios
