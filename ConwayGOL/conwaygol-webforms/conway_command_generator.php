<?php
$filestring = "<path_to_where_you_store_patterns>" . $_POST['patternID'];
$filehandle = fopen($filestring, 'w') or die("Can't open file");
for($row=0; $row<36; $row++)
{
    for($col=0; $col<36;$col++)
    {
        $key = "cellr" . $row . "c" . $col;
        if ($_POST[$key] == 'on')
        {
            fwrite($filehandle, $row . "," . $col . "\n");
        }
    }
}
fclose($filehandle);
?>

<p><span style="font-size: larger;">The starting pattern is ready to load.</span></p>
<p>To load the starting pattern:</p>
<p><ul><li>Move your avatar into the region where the Game of Life module is installed.</li><li>Paste the following text into the chat window:</li></ul></p>
<p><blockquote style="font-size: larger;"><b>/9 <?php echo $_POST['patternID'];?></b></blockquote></p>
<p>To start the Game of Life:</p>
<p><ul><li>Paste the following text into the chat window:</li></ul></p>
<p><blockquote style="font-size: larger;"><b>/9 start</b></blockquote></p>

